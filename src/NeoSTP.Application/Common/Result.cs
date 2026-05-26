namespace NeoSTP.Application.Common;

public class Result
{
    public bool IsSuccess { get; protected init; }
    public string? Error { get; protected init; }
    public string? ErrorCode { get; protected init; }
    public IReadOnlyList<string> ValidationErrors { get; protected init; } = Array.Empty<string>();

    public bool IsFailure => !IsSuccess;

    public static Result Ok() => new() { IsSuccess = true };

    public static Result Fail(string error, string? errorCode = null, IEnumerable<string>? validationErrors = null)
        => new()
        {
            IsSuccess = false,
            Error = error,
            ErrorCode = errorCode,
            ValidationErrors = validationErrors?.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>(),
        };
}

public class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };

    public new static Result<T> Fail(string error, string? errorCode = null, IEnumerable<string>? validationErrors = null)
        => new()
        {
            IsSuccess = false,
            Error = error,
            ErrorCode = errorCode,
            ValidationErrors = validationErrors?.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>(),
        };
}
