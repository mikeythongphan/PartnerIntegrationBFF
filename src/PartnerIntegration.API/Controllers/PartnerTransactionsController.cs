using MediatR;
using Microsoft.AspNetCore.Mvc;
using PartnerIntegration.Application.Commands.SubmitTransaction;
using PartnerIntegration.Application.DTOs;
using PartnerIntegration.Application.Queries.GetTransactionStatus;

namespace PartnerIntegration.API.Controllers;

/// <summary>
/// Partner Transaction BFF — thin controller.
/// The controller's only job is HTTP translation:
///   - Map HTTP request → Command/Query
///   - Dispatch via IMediator
///   - Map Result → HTTP response
/// Zero business logic lives here.
/// </summary>
[ApiController]
[Route("api/v1/partner")]
[Produces("application/json")]
public class PartnerTransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PartnerTransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Submit a partner transaction for processing.
    /// </summary>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(PartnerTransactionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SubmitTransaction(
        [FromBody] SubmitTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitTransactionCommand(
            request.PartnerId,
            request.TransactionReference,
            request.Amount,
            request.Currency,
            request.Timestamp);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "VALIDATION_ERROR" => UnprocessableEntity(new
                {
                    errorCode = result.ErrorCode,
                    message = result.ErrorMessage,
                    validationErrors = result.ValidationErrors
                }),
                "PARTNER_VERIFICATION_FAILED" or "PARTNER_INACTIVE" =>
                    StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        errorCode = result.ErrorCode,
                        message = result.ErrorMessage
                    }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = result.ErrorCode,
                    message = result.ErrorMessage
                })
            };
        }

        return Accepted(result.Value);
    }

    /// <summary>
    /// Get the status of a previously submitted transaction (Query side).
    /// </summary>
    [HttpGet("transactions/{transactionId:guid}")]
    [ProducesResponseType(typeof(TransactionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTransactionStatus(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var query = new GetTransactionStatusQuery(transactionId);
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage });

        return Ok(result.Value);
    }
}

// HTTP-specific DTO — decoupled from the Command so HTTP shape can evolve independently
public record SubmitTransactionRequest(
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTime Timestamp
);
