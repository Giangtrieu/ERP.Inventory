namespace ERP.Inventory.Application.Common;

public sealed class ServiceResult<T>
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public T? Data { get; private init; }
    public IReadOnlyCollection<string> Errors { get; private init; } = Array.Empty<string>();

    public static ServiceResult<T> Ok(T data, string message = "")
    {
        return new ServiceResult<T> { Success = true, Data = data, Message = message };
    }

    public static ServiceResult<T> Fail(string error)
    {
        return new ServiceResult<T> { Success = false, Errors = new[] { error }, Message = error };
    }

    public static ServiceResult<T> Fail(IEnumerable<string> errors)
    {
        var list = errors.ToArray();
        return new ServiceResult<T> { Success = false, Errors = list, Message = list.FirstOrDefault() ?? "Operation failed." };
    }
}

