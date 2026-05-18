using Microsoft.Extensions.Options;

namespace PartnerIntegration.API.Middleware;

/// <summary>
/// Simple API Key authentication middleware.
/// In production, prefer JWT Bearer tokens (see Extensions/JwtExtensions.cs).
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ApiKeySettings _settings;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IOptions<ApiKeySettings> settings,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health checks, Swagger, and mock endpoints
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/mock", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var extractedApiKey))
        {
            _logger.LogWarning("API key missing for request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = "UNAUTHORIZED",
                message = "API key is missing. Provide it via the 'X-Api-Key' header."
            });
            return;
        }

        if (!_settings.ValidKeys.Contains(extractedApiKey.ToString()))
        {
            _logger.LogWarning("Invalid API key used for request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = "FORBIDDEN",
                message = "Invalid API key."
            });
            return;
        }

        await _next(context);
    }
}

public class ApiKeySettings
{
    public const string SectionName = "ApiKeyAuthentication";
    public HashSet<string> ValidKeys { get; set; } = new();
}
