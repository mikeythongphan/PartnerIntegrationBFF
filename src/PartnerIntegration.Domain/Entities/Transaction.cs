namespace PartnerIntegration.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public string PartnerId { get; private set; }
    public string TransactionReference { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public DateTime Timestamp { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Transaction() { } // EF Core

    public static Transaction Create(
        string partnerId,
        string transactionReference,
        decimal amount,
        string currency,
        DateTime timestamp)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            PartnerId = partnerId,
            TransactionReference = transactionReference,
            Amount = amount,
            Currency = currency,
            Timestamp = timestamp,
            CreatedAt = DateTime.UtcNow
        };
    }
}
