namespace PartnerIntegration.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class PartnerVerificationException : DomainException
{
    public string PartnerId { get; }

    public PartnerVerificationException(string partnerId)
        : base($"Partner '{partnerId}' could not be verified or is not active.")
    {
        PartnerId = partnerId;
    }
}

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public class MessageBrokerException : DomainException
{
    public MessageBrokerException(string message) : base(message) { }
}
