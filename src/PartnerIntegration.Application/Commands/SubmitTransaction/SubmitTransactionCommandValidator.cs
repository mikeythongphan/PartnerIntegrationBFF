using FluentValidation;

namespace PartnerIntegration.Application.Commands.SubmitTransaction;

public sealed class SubmitTransactionCommandValidator : AbstractValidator<SubmitTransactionCommand>
{
    private static readonly HashSet<string> ValidCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY", "SEK", "NOK",
        "DKK", "SGD", "HKD", "KRW", "INR", "BRL", "MXN", "ZAR", "NZD", "TRY"
    };

    public SubmitTransactionCommandValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty().WithMessage("PartnerId is required.")
            .MaximumLength(50).WithMessage("PartnerId must not exceed 50 characters.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty().WithMessage("TransactionReference is required.")
            .MaximumLength(100).WithMessage("TransactionReference must not exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be exactly 3 characters (ISO 4217).")
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage("Currency is not a valid ISO 4217 currency code.");

        RuleFor(x => x.Timestamp)
            .NotEmpty().WithMessage("Timestamp is required.")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be in the future.");
    }
}
