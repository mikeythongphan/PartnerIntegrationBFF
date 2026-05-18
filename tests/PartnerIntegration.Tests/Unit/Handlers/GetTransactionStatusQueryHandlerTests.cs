using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PartnerIntegration.Application.Queries.GetTransactionStatus;
using Xunit;

namespace PartnerIntegration.Tests.Unit.Handlers;

public class GetTransactionStatusQueryHandlerTests
{
    private readonly Mock<ITransactionReadStore> _readStoreMock = new();
    private readonly Mock<ILogger<GetTransactionStatusQueryHandler>> _loggerMock = new();

    private GetTransactionStatusQueryHandler CreateSut() =>
        new(_readStoreMock.Object, _loggerMock.Object);

    private static TransactionStatusResponse SampleRecord(Guid id) => new(
        id, "P-1001", "TXN-001", 100m, "USD", DateTime.UtcNow.AddMinutes(-5), "Queued");

    [Fact]
    public async Task Handle_ExistingTransaction_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        _readStoreMock
            .Setup(s => s.FindByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleRecord(id));

        var result = await CreateSut().Handle(new GetTransactionStatusQuery(id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TransactionId.Should().Be(id);
        result.Value.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Handle_NonExistentTransaction_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _readStoreMock
            .Setup(s => s.FindByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionStatusResponse?)null);

        var result = await CreateSut().Handle(new GetTransactionStatusQuery(id), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TRANSACTION_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_QueryCallsReadStoreOnce()
    {
        var id = Guid.NewGuid();
        _readStoreMock
            .Setup(s => s.FindByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionStatusResponse?)null);

        await CreateSut().Handle(new GetTransactionStatusQuery(id), default);

        _readStoreMock.Verify(s => s.FindByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
