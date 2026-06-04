namespace ERP.Inventory.Application.Common;

public sealed class ServiceResult<T>
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public T? Data { get; private init; }
    public IReadOnlyCollection<string> Errors { get; private init; } = Array.Empty<string>();
    public string? ErrorType { get; private init; }
    public string? CorrelationId { get; private init; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; private init; } = new Dictionary<string, string[]>();

    public static ServiceResult<T> Ok(T data, string message = "")
    {
        return new ServiceResult<T> { Success = true, Data = data, Message = message };
    }

    public static ServiceResult<T> Fail(string error)
    {
        return new ServiceResult<T> { Success = false, Errors = new[] { error }, Message = error };
    }

    public static ServiceResult<T> Fail(string error, string errorType, string? correlationId = null)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Errors = new[] { error },
            Message = error,
            ErrorType = errorType,
            CorrelationId = correlationId
        };
    }

    public static ServiceResult<T> Fail(IEnumerable<string> errors)
    {
        var list = errors.ToArray();
        return new ServiceResult<T> { Success = false, Errors = list, Message = list.FirstOrDefault() ?? "Operation failed." };
    }

    public static ServiceResult<T> Fail(IEnumerable<string> errors, string errorType, string? correlationId = null)
    {
        var list = errors.ToArray();
        return new ServiceResult<T>
        {
            Success = false,
            Errors = list,
            Message = list.FirstOrDefault() ?? "Operation failed.",
            ErrorType = errorType,
            CorrelationId = correlationId
        };
    }

    public static ServiceResult<T> Fail(
        string message,
        IReadOnlyDictionary<string, string[]> fieldErrors,
        string errorType = "BusinessValidation",
        string? correlationId = null)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Errors = fieldErrors.SelectMany(x => x.Value).ToArray(),
            Message = message,
            ErrorType = errorType,
            CorrelationId = correlationId,
            FieldErrors = fieldErrors
        };
    }
}
