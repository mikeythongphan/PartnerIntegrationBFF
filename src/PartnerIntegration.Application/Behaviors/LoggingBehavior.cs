using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PartnerIntegration.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs the start, end, and duration of every
/// command/query. This eliminates repetitive logging boilerplate from handlers.
///
/// Pipeline order: Request → [LoggingBehavior] → [ValidationBehavior] → Handler
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("→ Handling {RequestName} {@Request}", requestName, request);

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next();
            sw.Stop();

            _logger.LogInformation(
                "← Handled {RequestName} in {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "✗ {RequestName} failed after {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
