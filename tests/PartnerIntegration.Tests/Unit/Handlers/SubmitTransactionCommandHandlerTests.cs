using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PartnerIntegration.Application.Commands.SubmitTransaction;
using PartnerIntegration.Application.DTOs;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Application.Queries.GetTransactionStatus;
using PartnerIntegration.Domain.Exceptions;
using Xunit;

namespace PartnerIntegration.Tests.Unit.Handlers;

public class SubmitTransactionCommandHandlerTests
{
    private readonly Mock<IPartnerVerificationService> _verificationMock = new();
    private readonly Mock<IMessagePublisher> _publisherMock = new();
    private readonly Mock<ITransactionWriteStore> _writeStoreMock = new();
    private readonly Mock<ILogger<SubmitTransactionCommandHandler>> _loggerMock = new();

    private SubmitTransactionCommandHandler CreateSut() => new(
        _verificationMock.Object,
        _publisherMock.Object,
        _writeStoreMock.Object,
        _loggerMock.Object);

    private static SubmitTransactionCommand ValidCommand() => new(
        PartnerId: "P-1001",
        TransactionReference: "TXN-99823",
        Amount: 250m,
        Currency: "USD",
        Timestamp: DateTime.UtcNow.AddMinutes(-5));

    private void SetupActivePartner(string partnerId = "P-1001") =>
        _verificationMock
            .Setup(s => s.VerifyPartnerAsync(partnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerVerificationResponse(partnerId, true, "Acme Corp"));

    private void SetupPublishSuccess() =>
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<TransactionMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupWriteStoreSuccess() =>
        _writeStoreMock
            .Setup(s => s.SaveAsync(It.IsAny<TransactionStatusResponse>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    // ─── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        SetupActivePartner();
        SetupPublishSuccess();
        SetupWriteStoreSuccess();

        var result = await CreateSut().Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Accepted");
        result.Value.TransactionId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesExactlyOnce()
    {
        SetupActivePartner();
        SetupPublishSuccess();
        SetupWriteStoreSuccess();

        await CreateSut().Handle(ValidCommand(), default);

        _publisherMock.Verify(
            p => p.PublishAsync(
                It.IsAny<TransactionMessage>(),
                "partner-transactions",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ProjectsToReadStore()
    {
        SetupActivePartner();
        SetupPublishSuccess();
        SetupWriteStoreSuccess();

        await CreateSut().Handle(ValidCommand(), default);

        _writeStoreMock.Verify(
            s => s.SaveAsync(It.IsAny<TransactionStatusResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_MessageContainsCorrectData()
    {
        var command = ValidCommand();
        SetupActivePartner(command.PartnerId);
        SetupWriteStoreSuccess();

        TransactionMessage? captured = null;
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<TransactionMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionMessage, string, CancellationToken>((msg, _, _) => captured = msg)
            .Returns(Task.CompletedTask);

        await CreateSut().Handle(command, default);

        captured.Should().NotBeNull();
        captured!.PartnerId.Should().Be(command.PartnerId);
        captured.Amount.Should().Be(command.Amount);
        captured.Currency.Should().Be(command.Currency);
    }

    // ─── Partner verification failures ─────────────────────────────────────────

    [Fact]
    public async Task Handle_PartnerVerificationThrows_ReturnsFailureResult()
    {
        _verificationMock
            .Setup(s => s.VerifyPartnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PartnerVerificationException("P-1001"));

        var result = await CreateSut().Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARTNER_VERIFICATION_FAILED");
    }

    [Fact]
    public async Task Handle_InactivePartner_ReturnsFailureResult()
    {
        _verificationMock
            .Setup(s => s.VerifyPartnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerVerificationResponse("P-1001", false, "Inactive Inc"));

        var result = await CreateSut().Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARTNER_INACTIVE");
    }

    [Fact]
    public async Task Handle_PartnerVerificationFails_DoesNotPublish()
    {
        _verificationMock
            .Setup(s => s.VerifyPartnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PartnerVerificationException("P-1001"));

        await CreateSut().Handle(ValidCommand(), default);

        _publisherMock.Verify(
            p => p.PublishAsync(It.IsAny<TransactionMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PartnerVerificationFails_DoesNotWriteToReadStore()
    {
        _verificationMock
            .Setup(s => s.VerifyPartnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PartnerVerificationException("P-1001"));

        await CreateSut().Handle(ValidCommand(), default);

        _writeStoreMock.Verify(
            s => s.SaveAsync(It.IsAny<TransactionStatusResponse>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Message broker failures ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_PublisherThrows_PropagatesException()
    {
        SetupActivePartner();
        _publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<TransactionMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MessageBrokerException("Broker down"));

        var act = () => CreateSut().Handle(ValidCommand(), default);

        await act.Should().ThrowAsync<MessageBrokerException>();
    }
}
