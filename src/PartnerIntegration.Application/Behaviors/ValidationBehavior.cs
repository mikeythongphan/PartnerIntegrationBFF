using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PartnerIntegration.Application.Common;

namespace PartnerIntegration.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation BEFORE the handler.
/// This removes ALL validation code from individual handlers — they can
/// assume the request is already valid when they execute.
///
/// Pipeline order: Request → [LoggingBehavior] → [ValidationBehavior] → Handler
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        _logger.LogDebug("Validating {RequestType}", typeof(TRequest).Name);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray()
            );

        _logger.LogWarning(
            "Validation failed for {RequestType}: {@Errors}",
            typeof(TRequest).Name, errors);

        // Return a Result failure if TResponse is Result<T>, else throw
        var responseType = typeof(TResponse);
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(innerType)
                .GetMethod(nameof(Result<object>.ValidationFailure))!;

            return (TResponse)failureMethod.Invoke(null, new object[] { errors })!;
        }

        throw new Domain.Exceptions.ValidationException(errors);
    }
}
