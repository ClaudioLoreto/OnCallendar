namespace OnCallendar.Application.Common;

/// <summary>Error categories returned by Application services.</summary>
public enum ServiceErrorKind
{
    NotFound,
    Forbidden,
    Conflict,
    ValidationError,
    BlockedByRules,
    NeedsConfirmation,
}

/// <summary>Non-generic result for void-returning operations.</summary>
public sealed class ServiceResult
{
    public ServiceErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public object? Details { get; }
    public bool IsSuccess => ErrorKind is null;

    private ServiceResult() { }
    private ServiceResult(ServiceErrorKind kind, string message, object? details = null)
    {
        ErrorKind = kind;
        ErrorMessage = message;
        Details = details;
    }

    public static ServiceResult Ok() => new();
    public static ServiceResult NotFound(string msg = "Non trovato.") => new(ServiceErrorKind.NotFound, msg);
    public static ServiceResult Forbidden() => new(ServiceErrorKind.Forbidden, "Accesso negato.");
    public static ServiceResult Conflict(string msg) => new(ServiceErrorKind.Conflict, msg);
    public static ServiceResult ValidationError(string msg) => new(ServiceErrorKind.ValidationError, msg);
}

/// <summary>Generic result wrapping a value or a typed error.</summary>
public sealed class ServiceResult<T>
{
    public T? Value { get; }
    public ServiceErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public object? Details { get; }
    public bool IsSuccess => ErrorKind is null;

    private ServiceResult(T value) => Value = value;
    private ServiceResult(ServiceErrorKind kind, string message, object? details = null)
    {
        ErrorKind = kind;
        ErrorMessage = message;
        Details = details;
    }

    public static ServiceResult<T> Ok(T value) => new(value);
    public static ServiceResult<T> NotFound(string msg = "Non trovato.") => new(ServiceErrorKind.NotFound, msg);
    public static ServiceResult<T> Forbidden() => new(ServiceErrorKind.Forbidden, "Accesso negato.");
    public static ServiceResult<T> Conflict(string msg) => new(ServiceErrorKind.Conflict, msg);
    public static ServiceResult<T> ValidationError(string msg) => new(ServiceErrorKind.ValidationError, msg);
    public static ServiceResult<T> Blocked(string msg, object violations) => new(ServiceErrorKind.BlockedByRules, msg, violations);
    public static ServiceResult<T> NeedsConfirmation(string msg, object violations) => new(ServiceErrorKind.NeedsConfirmation, msg, violations);
}
