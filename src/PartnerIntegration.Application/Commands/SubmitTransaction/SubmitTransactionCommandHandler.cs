using MediatR;
using Microsoft.Extensions.Logging;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Application.DTOs;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Application.Queries.GetTransactionStatus;
using PartnerIntegration.Domain.Entities;
using PartnerIntegration.Domain.Exceptions;

namespace PartnerIntegration.Application.Commands.SubmitTransaction;

/// <summary>
/// Handles the SubmitTransactionCommand.
/// Single responsibility: orchestrate partner verification → domain creation → queue publish → project to read model.
/// Validation is handled upstream by ValidationBehavior — this handler can assume the command is valid.
/// </summary>
public sealed class SubmitTransactionCommandHandler
    : IRequestHandler<SubmitTransactionCommand, Result<PartnerTransactionResponse>>
{
    private const string TransactionQueueName = "partner-transactions";

    private readonly IPartnerVerificationService _partnerVerificationService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ITransactionWriteStore _writeStore;
    private readonly ILogger<SubmitTransactionCommandHandler> _logger;

    public SubmitTransactionCommandHandler(
        IPartnerVerificationService partnerVerificationService,
        IMessagePublisher messagePublisher,
        ITransactionWriteStore writeStore,
        ILogger<SubmitTransactionCommandHandler> logger)
    {
        _partnerVerificationService = partnerVerificationService;
        _messagePublisher = messagePublisher;
        _writeStore = writeStore;
        _logger = logger;
    }

    public async Task<Result<PartnerTransactionResponse>> Handle(
        SubmitTransactionCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling SubmitTransactionCommand — partner {PartnerId}, ref {TransactionRef}",
            command.PartnerId, command.TransactionReference);

        // Step 1: Verify partner identity (Polly resilience applied in infrastructure)
        PartnerVerificationResponse partnerVerification;
        try
        {
            partnerVerification = await _partnerVerificationService
                .VerifyPartnerAsync(command.PartnerId, cancellationToken);
        }
        catch (PartnerVerificationException)
        {
            return Result<PartnerTransactionResponse>.Failure(
                "PARTNER_VERIFICATION_FAILED",
                $"Partner '{command.PartnerId}' could not be verified.");
        }

        if (!partnerVerification.IsActive)
        {
            _logger.LogWarning("Partner {PartnerId} is inactive", command.PartnerId);
            return Result<PartnerTransactionResponse>.Failure(
                "PARTNER_INACTIVE",
                $"Partner '{command.PartnerId}' is not active.");
        }

        // Step 2: Create domain entity (encapsulates domain rules)
        var transaction = Transaction.Create(
            command.PartnerId,
            command.TransactionReference,
            command.Amount,
            command.Currency,
            command.Timestamp);

        // Step 3: Publish to message queue
        var message = new TransactionMessage(
            transaction.Id,
            transaction.PartnerId,
            transaction.TransactionReference,
            transaction.Amount,
            transaction.Currency,
            transaction.Timestamp,
            DateTime.UtcNow);

        await _messagePublisher.PublishAsync(message, TransactionQueueName, cancellationToken);

        // Step 4: Project into read model so queries can immediately find this transaction
        await _writeStore.SaveAsync(new TransactionStatusResponse(
            transaction.Id,
            transaction.PartnerId,
            transaction.TransactionReference,
            transaction.Amount,
            transaction.Currency,
            transaction.Timestamp,
            "Queued"), cancellationToken);

        _logger.LogInformation(
            "Transaction {TransactionId} queued successfully for partner {PartnerId}",
            transaction.Id, command.PartnerId);

        return Result<PartnerTransactionResponse>.Success(new PartnerTransactionResponse(
            transaction.Id,
            "Accepted",
            "Transaction has been validated and queued for processing."));
    }
}
