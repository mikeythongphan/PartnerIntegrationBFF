using PartnerIntegration.Application.Queries.GetTransactionStatus;

namespace PartnerIntegration.Application.Interfaces;

/// <summary>
/// Write-side projection store.
/// The command handler projects the new transaction into the read model
/// so queries can immediately find it after submission.
/// Concrete implementation lives in Infrastructure (InMemoryTransactionReadStore).
/// </summary>
public interface ITransactionWriteStore
{
    Task SaveAsync(TransactionStatusResponse record, CancellationToken cancellationToken = default);
}
