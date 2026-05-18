using MediatR;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Application.DTOs;

namespace PartnerIntegration.Application.Commands.SubmitTransaction;

/// <summary>
/// Command: intent to submit a partner transaction.
/// Immutable record — commands carry only the data needed to perform the action.
/// </summary>
public sealed record SubmitTransactionCommand(
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTime Timestamp
) : IRequest<Result<PartnerTransactionResponse>>;
