using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public static class SuperAdminSecurity
{
    public static string SuperAdminPassword = "PBKDF2$100000$q8tKn5PqrJL6zfW+bB42rg==$TcVQAKQJNtpQ3J9lTUrNBlc0WvBS+f6vo/78uaaYJn0="; 

    public static string Verify(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(SuperAdminPassword)) return "";


        return SuperAdminPassword;
    }
        //=> !string.IsNullOrWhiteSpace(SuperAdminPassword) 
        //   && string.Equals(password, SuperAdminPassword, StringComparison.Ordinal);
}

public sealed record LogErrorContext(
    string? Module = null,
    string? Action = null,
    string? PayloadJson = null,
    string? RequestPath = null,
    string? HttpMethod = null,
    string? UserId = null,
    string? UserName = null,
    string? ClientIp = null,
    string? Browser = null,
    SystemErrorCategory? Category = null,
    int? StatusCode = null,
    long? DurationMs = null,
    string? SafeMessage = null,
    string? Severity = null,
    string? CorrelationId = null);

public interface ILogErrorSystemService
{
    Task<LogErrorSystem> LogAsync(Exception exception, LogErrorContext context, CancellationToken cancellationToken = default);
}

public sealed class LogErrorSystemService : ILogErrorSystemService
{
    private const int MaxTextLength = 4000;
    private const int MaxPayloadLength = 16000;
    private readonly InventoryDbContext _db;

    public LogErrorSystemService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<LogErrorSystem> LogAsync(Exception exception, LogErrorContext context, CancellationToken cancellationToken = default)
    {
        var createdAt = DateTime.UtcNow;
        var category = context.Category ?? SystemErrorClassifier.Classify(exception, context.StatusCode);
        var sqlNumber = SystemErrorClassifier.TryReadSqlNumber(exception, out var number) ? number : (int?)null;
        var errorCode = !string.IsNullOrWhiteSpace(context.CorrelationId)
            ? context.CorrelationId!
            : $"{createdAt:yyyyMMdd}-{Guid.NewGuid():N}"[..12];
        var row = new LogErrorSystem
        {
            CreatedAt = createdAt,
            ErrorCode = errorCode,
            Category = category.ToString(),
            Severity = Trim(context.Severity, 30) ?? SeverityFor(category),
            UserId = Trim(context.UserId, 100),
            UserName = Trim(context.UserName, 200),
            RequestPath = Trim(context.RequestPath, 500),
            HttpMethod = Trim(context.HttpMethod, 20),
            Module = Trim(context.Module, 100),
            Action = Trim(context.Action, 100),
            ErrorMessage = Trim(context.SafeMessage ?? exception.Message, MaxTextLength) ?? exception.GetType().Name,
            TechnicalMessage = Trim(exception.Message, MaxTextLength),
            ExceptionType = Trim(exception.GetType().FullName ?? exception.GetType().Name, 300),
            SqlErrorNumber = sqlNumber,
            StatusCode = context.StatusCode,
            DurationMs = context.DurationMs,
            InnerException = Trim(exception.InnerException?.ToString(), MaxTextLength),
            StackTrace = Trim(exception.ToString(), MaxPayloadLength),
            PayloadJson = Trim(context.PayloadJson, MaxPayloadLength),
            ClientIp = Trim(context.ClientIp, 100),
            Browser = Trim(context.Browser, 500),
            IsResolved = false
        };

        _db.LogErrorSystems.Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        return row;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string SeverityFor(SystemErrorCategory category)
        => category is SystemErrorCategory.BusinessValidation
            or SystemErrorCategory.BusinessDependency
            or SystemErrorCategory.Unauthorized
            or SystemErrorCategory.Forbidden
            or SystemErrorCategory.NotFound
            ? "Warning"
            : "Error";
}

public static class SystemErrorClassifier
{
    public static SystemErrorCategory Classify(Exception exception, int? statusCode = null)
    {
        if (statusCode.HasValue)
        {
            return statusCode.Value switch
            {
                401 => SystemErrorCategory.Unauthorized,
                403 => SystemErrorCategory.Forbidden,
                404 => SystemErrorCategory.NotFound,
                408 or 504 => SystemErrorCategory.Timeout,
                _ => statusCode.Value >= 500 ? SystemErrorCategory.UnhandledException : SystemErrorCategory.BusinessValidation
            };
        }

        if (exception is TimeoutException or TaskCanceledException or OperationCanceledException)
        {
            return SystemErrorCategory.Timeout;
        }

        if (TryReadSqlNumber(exception, out var number))
        {
            return number switch
            {
                -2 => SystemErrorCategory.Timeout,
                1205 => SystemErrorCategory.Deadlock,
                _ => exception is DbUpdateException ? SystemErrorCategory.DbUpdateException : SystemErrorCategory.UnhandledException
            };
        }

        return exception is DbUpdateException
            ? SystemErrorCategory.DbUpdateException
            : SystemErrorCategory.UnhandledException;
    }

    public static bool TryReadSqlNumber(Exception? exception, out int number)
    {
        number = 0;
        while (exception != null)
        {
            var property = exception.GetType().GetProperty("Number");
            if (property?.GetValue(exception) is int value)
            {
                number = value;
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
