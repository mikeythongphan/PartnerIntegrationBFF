using Microsoft.AspNetCore.Mvc;
using PartnerIntegration.Application.DTOs;

namespace PartnerIntegration.API.Controllers;

/// <summary>
/// Mock Partner Verification API.
/// Simulates a real third-party partner verification service.
/// - 30% chance of throwing TimeoutException
/// - 70% chance of returning a valid response
/// </summary>
[ApiController]
[Route("api/v1/mock")]
[Produces("application/json")]
public class MockPartnerVerificationController : ControllerBase
{
    private static readonly Random _random = Random.Shared;

    private static readonly Dictionary<string, (bool IsActive, string Name)> _mockPartners = new()
    {
        ["P-1001"] = (true, "Acme Corp"),
        ["P-1002"] = (true, "GlobalPay Ltd"),
        ["P-1003"] = (false, "Inactive Partner"),
        ["P-9999"] = (true, "Test Partner"),
    };

    private readonly ILogger<MockPartnerVerificationController> _logger;

    public MockPartnerVerificationController(ILogger<MockPartnerVerificationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verifies a partner by ID. Randomly fails with a timeout 30% of the time.
    /// </summary>
    [HttpGet("partners/{partnerId}/verify")]
    [ProducesResponseType(typeof(PartnerVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
    public async Task<IActionResult> VerifyPartner(string partnerId)
    {
        _logger.LogInformation("Mock: Verifying partner {PartnerId}", partnerId);

        // Simulate 30% timeout failure
        if (_random.NextDouble() < 0.30)
        {
            _logger.LogWarning("Mock: Simulating TimeoutException for partner {PartnerId}", partnerId);

            // Small delay to simulate a slow response before timing out
            await Task.Delay(100);
            throw new TimeoutException($"Mock: Partner verification service timed out for partner '{partnerId}'.");
        }

        if (!_mockPartners.TryGetValue(partnerId, out var partnerInfo))
        {
            _logger.LogWarning("Mock: Partner {PartnerId} not found", partnerId);
            return NotFound(new { message = $"Partner '{partnerId}' not found." });
        }

        var response = new PartnerVerificationResponse(
            partnerId,
            partnerInfo.IsActive,
            partnerInfo.Name);

        _logger.LogInformation("Mock: Partner {PartnerId} verified. IsActive={IsActive}", partnerId, partnerInfo.IsActive);

        return Ok(response);
    }
}
