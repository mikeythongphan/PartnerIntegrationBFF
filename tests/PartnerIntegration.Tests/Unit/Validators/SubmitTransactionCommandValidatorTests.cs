using FluentAssertions;
using PartnerIntegration.Application.Commands.SubmitTransaction;
using Xunit;

namespace PartnerIntegration.Tests.Unit.Validators;

public class SubmitTransactionCommandValidatorTests
{
    private readonly SubmitTransactionCommandValidator _sut = new();

    private static SubmitTransactionCommand Valid() => new(
        PartnerId: "P-1001",
        TransactionReference: "TXN-99823",
        Amount: 250.00m,
        Currency: "USD",
        Timestamp: DateTime.UtcNow.AddMinutes(-5)
    );

    [Fact]
    public async Task Validate_ValidCommand_ShouldPass()
    {
        var result = await _sut.ValidateAsync(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Validate_EmptyPartnerId_ShouldFail(string? id)
    {
        var result = await _sut.ValidateAsync(Valid() with { PartnerId = id! });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTransactionCommand.PartnerId));
    }

    [Fact]
    public async Task Validate_PartnerIdTooLong_ShouldFail()
    {
        var result = await _sut.ValidateAsync(Valid() with { PartnerId = new string('X', 51) });
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-999)]
    public async Task Validate_AmountNotGreaterThanZero_ShouldFail(decimal amount)
    {
        var result = await _sut.ValidateAsync(Valid() with { Amount = amount });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTransactionCommand.Amount));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1_000_000)]
    public async Task Validate_AmountGreaterThanZero_ShouldPass(decimal amount)
    {
        var result = await _sut.ValidateAsync(Valid() with { Amount = amount });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(SubmitTransactionCommand.Amount));
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("US")]
    [InlineData("XYZ")]
    [InlineData("123")]
    public async Task Validate_InvalidCurrency_ShouldFail(string currency)
    {
        var result = await _sut.ValidateAsync(Valid() with { Currency = currency });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTransactionCommand.Currency));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("jpy")] // case-insensitive
    public async Task Validate_ValidCurrency_ShouldPass(string currency)
    {
        var result = await _sut.ValidateAsync(Valid() with { Currency = currency });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(SubmitTransactionCommand.Currency));
    }

    [Fact]
    public async Task Validate_FutureTimestamp_ShouldFail()
    {
        var result = await _sut.ValidateAsync(Valid() with { Timestamp = DateTime.UtcNow.AddHours(1) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitTransactionCommand.Timestamp));
    }

    [Fact]
    public async Task Validate_MultipleInvalidFields_ShouldReturnAllErrors()
    {
        var bad = new SubmitTransactionCommand("", "", -1m, "INVALID", DateTime.UtcNow.AddHours(2));
        var result = await _sut.ValidateAsync(bad);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }
}
