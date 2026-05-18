namespace PartnerIntegration.Application.Common;

/// <summary>
/// Discriminated union result type.
/// Handlers return Result&lt;T&gt; instead of throwing exceptions for expected failures,
/// keeping exception handling reserved for truly unexpected errors.
/// </summary>
public class Result<T>
{
    public T? Value { get; }
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    private Result(string errorCode, string errorMessage, IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        IsSuccess = false;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ValidationErrors = validationErrors;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string errorCode, string errorMessage) =>
        new(errorCode, errorMessage);

    public static Result<T> ValidationFailure(IReadOnlyDictionary<string, string[]> errors) =>
        new("VALIDATION_ERROR", "One or more validation errors occurred.", errors);
}
