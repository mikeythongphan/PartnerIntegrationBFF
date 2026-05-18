using System.Collections.Concurrent;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Application.Queries.GetTransactionStatus;

namespace PartnerIntegration.Infrastructure.ReadStores;

/// <summary>
/// In-memory read store implementing both read and write sides.
/// Registered as singleton so the same dictionary is shared across requests.
///
/// Production replacement: a Redis cache, Elasticsearch index,
/// or a separate read-optimized SQL view populated by an event consumer.
/// </summary>
public sealed class InMemoryTransactionReadStore
    : ITransactionReadStore, ITransactionWriteStore
{
    private static readonly ConcurrentDictionary<Guid, TransactionStatusResponse> _store = new();

    public Task<TransactionStatusResponse?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(
        TransactionStatusResponse record,
        CancellationToken cancellationToken = default)
    {
        _store[record.TransactionId] = record;
        return Task.CompletedTask;
    }
}
