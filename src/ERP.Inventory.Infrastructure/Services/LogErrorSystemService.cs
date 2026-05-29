using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Infrastructure.Data;

namespace ERP.Inventory.Infrastructure.Services;

public static class SuperAdminSecurity
{
    public static string SuperAdminPassword = "foxcon168";

    public static bool Verify(string? password)
        => !string.IsNullOrWhiteSpace(SuperAdminPassword)
           && string.Equals(password, SuperAdminPassword, StringComparison.Ordinal);
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
    string? Browser = null);

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
        var row = new LogErrorSystem
        {
            CreatedAt = DateTime.UtcNow,
            UserId = Trim(context.UserId, 100),
            UserName = Trim(context.UserName, 200),
            RequestPath = Trim(context.RequestPath, 500),
            HttpMethod = Trim(context.HttpMethod, 20),
            Module = Trim(context.Module, 100),
            Action = Trim(context.Action, 100),
            ErrorMessage = Trim(exception.Message, MaxTextLength) ?? exception.GetType().Name,
            InnerException = Trim(exception.InnerException?.ToString(), MaxTextLength),
            StackTrace = Trim(exception.ToString(), MaxPayloadLength),
            PayloadJson = Trim(context.PayloadJson, MaxPayloadLength),
            ClientIp = Trim(context.ClientIp, 100),
            Browser = Trim(context.Browser, 500),
            IsResolved = false
        };

        _db.LogErrorSystems.Add(row);
        await _db.SaveChangesAsync(cancellationToken);

        row.ErrorCode = $"ERR-{row.CreatedAt:yyyyMMdd}-{row.Id:D8}";
        await _db.SaveChangesAsync(cancellationToken);

        return row;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
