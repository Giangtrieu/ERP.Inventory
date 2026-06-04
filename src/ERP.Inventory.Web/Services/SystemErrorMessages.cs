using ERP.Inventory.Domain.Enums;
using System.Security.Claims;

namespace ERP.Inventory.Web.Services;

public enum SystemErrorKind
{
    System,
    Timeout,
    Validation,
    OperationFailure,
    BusinessDependency,
    Unauthorized,
    Forbidden,
    NotFound,
    Deadlock,
    DbUpdateException
}

public static class SystemErrorMessages
{
    public static string Create(HttpContext context, string errorCode, Exception? exception = null)
    {
        var language = ResolveLanguage(context);
        var key = IsTimeout(exception) ? "SystemError.TimeoutMessage" : "SystemError.UserMessage";
        return string.Format(LocalizationCatalog.Text(language, key), errorCode);
    }

    public static string Create(HttpContext context, string errorCode, SystemErrorCategory category)
    {
        var language = ResolveLanguage(context);
        var key = category switch
        {
            SystemErrorCategory.BusinessValidation => "SystemError.ValidationMessage",
            SystemErrorCategory.BusinessDependency => "SystemError.BusinessDependencyMessage",
            SystemErrorCategory.Timeout => "SystemError.TimeoutMessage",
            SystemErrorCategory.Deadlock => "SystemError.DeadlockMessage",
            SystemErrorCategory.Unauthorized => "SystemError.UnauthorizedMessage",
            SystemErrorCategory.Forbidden => "SystemError.ForbiddenMessage",
            SystemErrorCategory.NotFound => "SystemError.NotFoundMessage",
            SystemErrorCategory.DbUpdateException => "SystemError.DbUpdateMessage",
            _ => "SystemError.UserMessage"
        };

        return string.Format(LocalizationCatalog.Text(language, key), errorCode);
    }

    public static string CreateForFailure(HttpContext context, string errorCode, SystemErrorKind kind)
    {
        var language = ResolveLanguage(context);
        var key = kind switch
        {
            SystemErrorKind.Validation => "SystemError.ValidationMessage",
            SystemErrorKind.BusinessDependency => "SystemError.BusinessDependencyMessage",
            SystemErrorKind.Unauthorized => "SystemError.UnauthorizedMessage",
            SystemErrorKind.Forbidden => "SystemError.ForbiddenMessage",
            SystemErrorKind.NotFound => "SystemError.NotFoundMessage",
            SystemErrorKind.Deadlock => "SystemError.DeadlockMessage",
            SystemErrorKind.DbUpdateException => "SystemError.DbUpdateMessage",
            SystemErrorKind.OperationFailure => "SystemError.OperationFailureMessage",
            SystemErrorKind.Timeout => "SystemError.TimeoutMessage",
            _ => "SystemError.UserMessage"
        };

        return string.Format(LocalizationCatalog.Text(language, key), errorCode);
    }

    public static string ResolveLanguage(HttpContext context)
    {
        var language = context.User.FindFirstValue("language")
                       ?? RequestFormValue(context.Request, "LanguageCode")
                       ?? context.Request.Query["lang"].FirstOrDefault()
                       ?? context.Request.Query["language"].FirstOrDefault()
                       ?? context.Request.Headers.AcceptLanguage.FirstOrDefault()
                       ?? "vi";

        return language.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?.ToLowerInvariant() switch
            {
                "en" => "en",
                "zh" => "zh",
                _ => "vi"
            };
    }

    public static bool IsTimeout(Exception? exception)
    {
        if (exception == null) return false;
        if (exception is TimeoutException or TaskCanceledException) return true;

        if (exception is OperationCanceledException)
        {
            return true;
        }

        var typeName = exception.GetType().FullName ?? exception.GetType().Name;
        if (typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
            && TryReadSqlNumber(exception, out var number)
            && number == -2)
        {
            return true;
        }

        return IsTimeout(exception.InnerException);
    }

    public static string DefaultMessageKey(SystemErrorCategory category)
        => category switch
        {
            SystemErrorCategory.BusinessValidation => "Business validation failed.",
            SystemErrorCategory.BusinessDependency => "Business dependency blocked the operation.",
            SystemErrorCategory.Timeout => "Request timeout.",
            SystemErrorCategory.Deadlock => "Database deadlock.",
            SystemErrorCategory.Unauthorized => "Authentication is required.",
            SystemErrorCategory.Forbidden => "Access denied for current role.",
            SystemErrorCategory.NotFound => "Requested data was not found.",
            SystemErrorCategory.DbUpdateException => "Unexpected database update error.",
            _ => "Unhandled system exception."
        };

    private static bool TryReadSqlNumber(Exception exception, out int number)
    {
        number = 0;
        var property = exception.GetType().GetProperty("Number");
        if (property?.GetValue(exception) is int value)
        {
            number = value;
            return true;
        }

        return false;
    }

    private static string? RequestFormValue(HttpRequest request, string key)
        => request.HasFormContentType ? request.Form[key].FirstOrDefault() : null;
}
