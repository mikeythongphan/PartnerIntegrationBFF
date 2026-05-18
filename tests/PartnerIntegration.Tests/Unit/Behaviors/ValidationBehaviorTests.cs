using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using PartnerIntegration.Application.Behaviors;
using PartnerIntegration.Application.Commands.SubmitTransaction;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Application.DTOs;
using Xunit;

namespace PartnerIntegration.Tests.Unit.Behaviors;

public class ValidationBehaviorTests
{
    private readonly Mock<ILogger<ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>>> _loggerMock = new();

    private static SubmitTransactionCommand ValidCommand() => new(
        "P-1001", "TXN-001", 100m, "USD", DateTime.UtcNow.AddMinutes(-5));

    private static readonly RequestHandlerDelegate<Result<PartnerTransactionResponse>> NextDelegate =
        () => Task.FromResult(Result<PartnerTransactionResponse>.Success(
            new PartnerTransactionResponse(Guid.NewGuid(), "Accepted", "OK")));

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>(
            Enumerable.Empty<IValidator<SubmitTransactionCommand>>(),
            _loggerMock.Object);

        var result = await behavior.Handle(ValidCommand(), NextDelegate, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PassingValidator_CallsNext()
    {
        var validatorMock = new Mock<IValidator<SubmitTransactionCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SubmitTransactionCommand>>(), default))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>(
            new[] { validatorMock.Object },
            _loggerMock.Object);

        var result = await behavior.Handle(ValidCommand(), NextDelegate, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FailingValidator_ReturnsValidationFailureResult()
    {
        var failures = new[]
        {
            new ValidationFailure("Amount", "Amount must be greater than 0."),
            new ValidationFailure("Currency", "Currency is not valid.")
        };

        var validatorMock = new Mock<IValidator<SubmitTransactionCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SubmitTransactionCommand>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var behavior = new ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>(
            new[] { validatorMock.Object },
            _loggerMock.Object);

        var result = await behavior.Handle(ValidCommand(), NextDelegate, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ValidationErrors.Should().ContainKey("Amount");
        result.ValidationErrors.Should().ContainKey("Currency");
    }

    [Fact]
    public async Task Handle_FailingValidator_DoesNotCallNext()
    {
        var failures = new[] { new ValidationFailure("Amount", "Error") };
        var validatorMock = new Mock<IValidator<SubmitTransactionCommand>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SubmitTransactionCommand>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var nextCalled = false;
        RequestHandlerDelegate<Result<PartnerTransactionResponse>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<PartnerTransactionResponse>.Success(
                new PartnerTransactionResponse(Guid.NewGuid(), "OK", "OK")));
        };

        var behavior = new ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>(
            new[] { validatorMock.Object },
            _loggerMock.Object);

        await behavior.Handle(ValidCommand(), next, default);

        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleValidators_AggregatesAllErrors()
    {
        var validator1 = new Mock<IValidator<SubmitTransactionCommand>>();
        validator1
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SubmitTransactionCommand>>(), default))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Amount", "Error 1") }));

        var validator2 = new Mock<IValidator<SubmitTransactionCommand>>();
        validator2
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SubmitTransactionCommand>>(), default))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Currency", "Error 2") }));

        var behavior = new ValidationBehavior<SubmitTransactionCommand, Result<PartnerTransactionResponse>>(
            new[] { validator1.Object, validator2.Object },
            _loggerMock.Object);

        var result = await behavior.Handle(ValidCommand(), NextDelegate, default);

        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("Amount");
        result.ValidationErrors.Should().ContainKey("Currency");
    }
}
