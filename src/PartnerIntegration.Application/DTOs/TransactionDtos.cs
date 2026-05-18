namespace PartnerIntegration.Application.DTOs;

public record PartnerTransactionRequest(
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTime Timestamp
);

public record PartnerTransactionResponse(
    Guid TransactionId,
    string Status,
    string Message
);

public record PartnerVerificationResponse(
    string PartnerId,
    bool IsActive,
    string PartnerName
);

public record TransactionMessage(
    Guid TransactionId,
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTime Timestamp,
    DateTime QueuedAt
);
