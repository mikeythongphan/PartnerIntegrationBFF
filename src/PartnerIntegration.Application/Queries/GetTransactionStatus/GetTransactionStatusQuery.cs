using MediatR;
using Microsoft.Extensions.Logging;
using PartnerIntegration.Application.Common;

namespace PartnerIntegration.Application.Queries.GetTransactionStatus;

// ─── DTO ──────────────────────────────────────────────────────────────────────

public sealed record TransactionStatusResponse(
    Guid TransactionId,
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTime Timestamp,
    string Status
);

// ─── Query (read-side: no side effects) ───────────────────────────────────────

/// <summary>
/// Query: intent to read the status of a previously submitted transaction.
/// Queries NEVER mutate state — they only return data.
/// </summary>
public sealed record GetTransactionStatusQuery(Guid TransactionId)
    : IRequest<Result<TransactionStatusResponse>>;

// ─── Handler ──────────────────────────────────────────────────────────────────

/// <summary>
/// In a full CQRS setup this would read from a dedicated read-model / projection
/// (e.g. a separate read DB, Redis cache, or event-sourced snapshot).
/// Here we use an in-memory store to keep the demo self-contained.
/// </summary>
public sealed class GetTransactionStatusQueryHandler
    : IRequestHandler<GetTransactionStatusQuery, Result<TransactionStatusResponse>>
{
    // In production: inject ITransactionReadRepository (read-side DB context)
    private readonly ITransactionReadStore _readStore;
    private readonly ILogger<GetTransactionStatusQueryHandler> _logger;

    public GetTransactionStatusQueryHandler(
        ITransactionReadStore readStore,
        ILogger<GetTransactionStatusQueryHandler> logger)
    {
        _readStore = readStore;
        _logger = logger;
    }

    public async Task<Result<TransactionStatusResponse>> Handle(
        GetTransactionStatusQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying status for transaction {TransactionId}", query.TransactionId);

        var record = await _readStore.FindByIdAsync(query.TransactionId, cancellationToken);

        if (record is null)
        {
            return Result<TransactionStatusResponse>.Failure(
                "TRANSACTION_NOT_FOUND",
                $"Transaction '{query.TransactionId}' was not found.");
        }

        return Result<TransactionStatusResponse>.Success(record);
    }
}

// ─── Read-store abstraction (Query side only) ─────────────────────────────────

/// <summary>
/// Read-side repository. The query side is intentionally decoupled from
/// the write-side (no EF DbContext shared with commands).
/// </summary>
public interface ITransactionReadStore
{
    Task<TransactionStatusResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
