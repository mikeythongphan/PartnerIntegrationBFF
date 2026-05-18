using System.Text.Json;
using Microsoft.Extensions.Logging;
using PartnerIntegration.Application.DTOs;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Domain.Exceptions;

namespace PartnerIntegration.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client that calls the Partner Verification API.
/// Resilience (retry + circuit breaker) is configured via Polly in DI setup.
/// </summary>
public class PartnerVerificationService : IPartnerVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PartnerVerificationService> _logger;

    public PartnerVerificationService(HttpClient httpClient, ILogger<PartnerVerificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PartnerVerificationResponse> VerifyPartnerAsync(
        string partnerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying partner {PartnerId}", partnerId);

        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/v1/mock/partners/{Uri.EscapeDataString(partnerId)}/verify",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Partner verification API returned {StatusCode} for partner {PartnerId}",
                    response.StatusCode, partnerId);

                throw new PartnerVerificationException(partnerId);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PartnerVerificationResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? throw new PartnerVerificationException(partnerId);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout verifying partner {PartnerId}", partnerId);
            throw new PartnerVerificationException(partnerId);
        }
        catch (PartnerVerificationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying partner {PartnerId}", partnerId);
            throw new PartnerVerificationException(partnerId);
        }
    }
}
