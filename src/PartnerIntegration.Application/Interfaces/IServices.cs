using PartnerIntegration.Application.DTOs;

namespace PartnerIntegration.Application.Interfaces;

public interface IPartnerVerificationService
{
    Task<PartnerVerificationResponse> VerifyPartnerAsync(string partnerId, CancellationToken cancellationToken = default);
}

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string queueName, CancellationToken cancellationToken = default) where T : class;
}

// ITransactionService removed — replaced by CQRS:
//   Write side: SubmitTransactionCommand + SubmitTransactionCommandHandler
//   Read side:  GetTransactionStatusQuery  + GetTransactionStatusQueryHandler
