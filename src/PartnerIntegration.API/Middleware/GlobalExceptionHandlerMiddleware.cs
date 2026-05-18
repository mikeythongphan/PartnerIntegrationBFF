using System.Net;
using System.Text.Json;
using PartnerIntegration.Domain.Exceptions;

namespace PartnerIntegration.API.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorResponse) = exception switch
        {
            Domain.Exceptions.ValidationException ve => (
                HttpStatusCode.UnprocessableEntity,
                new ApiErrorResponse(
                    "VALIDATION_ERROR",
                    "One or more validation errors occurred.",
                    context.TraceIdentifier,
                    ve.Errors)),

            PartnerVerificationException pve => (
                HttpStatusCode.BadGateway,
                new ApiErrorResponse(
                    "PARTNER_VERIFICATION_FAILED",
                    pve.Message,
                    context.TraceIdentifier,
                    null)),

            MessageBrokerException mbe => (
                HttpStatusCode.ServiceUnavailable,
                new ApiErrorResponse(
                    "MESSAGE_BROKER_UNAVAILABLE",
                    "The message broker is currently unavailable. Please try again later.",
                    context.TraceIdentifier,
                    null)),

            OperationCanceledException => (
                HttpStatusCode.RequestTimeout,
                new ApiErrorResponse(
                    "REQUEST_TIMEOUT",
                    "The request timed out.",
                    context.TraceIdentifier,
                    null)),

            _ => (
                HttpStatusCode.InternalServerError,
                new ApiErrorResponse(
                    "INTERNAL_SERVER_ERROR",
                    "An unexpected error occurred.",
                    context.TraceIdentifier,
                    null))
        };

        // Log appropriately based on severity
        if ((int)statusCode >= 500)
        {
            _logger.LogError(exception,
                "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
                context.TraceIdentifier, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception,
                "Handled exception {ExceptionType}. TraceId: {TraceId}",
                exception.GetType().Name, context.TraceIdentifier);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
    }
}

public record ApiErrorResponse(
    string ErrorCode,
    string Message,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
);
