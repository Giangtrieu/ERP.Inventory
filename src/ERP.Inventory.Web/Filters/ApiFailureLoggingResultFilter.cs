using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Services;
using ERP.Inventory.Web.Services;
using ERP.Inventory.Web.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ERP.Inventory.Web.Filters;

public sealed class ApiFailureLoggingResultFilter : IAsyncResultFilter
{
    private const int MaxPayloadLength = 16000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex ErrorCodePattern = new(@"ERR-(?:\d{8}-\d{8}|\d{17}-[0-9a-fA-F]{18})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogErrorSystemService _errorLog;
    private readonly ILogger<ApiFailureLoggingResultFilter> _logger;

    public ApiFailureLoggingResultFilter(
        ILogErrorSystemService errorLog,
        ILogger<ApiFailureLoggingResultFilter> logger)
    {
        _errorLog = errorLog;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        try
        {
            if (TryGetJsonValue(context.Result, out var value)
                && value != null
                && TryCreateFailureEnvelope(value, out var envelope)
                && !HasErrorCode(envelope))
            {
                if (!TryAttachEmbeddedErrorCode(context, envelope))
                {
                    await LogAndAttachErrorCodeAsync(context, envelope);
                }

                context.Result = CreateJsonResult(context.Result, envelope);
            }
            else if (TryCreateStatusCodeFailureEnvelope(context.Result, out envelope))
            {
                await LogAndAttachErrorCodeAsync(context, envelope);
                context.Result = CreateJsonResult(context.Result, envelope);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API failure result filter failed for {Path}; returning original response.", context.HttpContext.Request.Path.Value);
        }

        await next();
    }

    private async Task LogAndAttachErrorCodeAsync(ResultExecutingContext context, JsonObject envelope)
    {
        var failure = ExtractFailure(envelope);
        var request = context.HttpContext.Request;
        var route = context.RouteData.Values;
        var payload = new
        {
            request = await ReadRequestBodyAsync(request),
            query = request.QueryString.HasValue ? request.QueryString.Value : null,
            response = envelope
        };

        try
        {
            var log = await _errorLog.LogAsync(new InvalidOperationException(
                    $"API returned {failure.Kind} failure: {failure.Message}"),
                new LogErrorContext(
                    Module: route.TryGetValue("controller", out var controller) ? controller?.ToString() : null,
                    Action: route.TryGetValue("action", out var action) ? action?.ToString() : null,
                    PayloadJson: JsonSerializer.Serialize(payload, JsonOptions),
                    RequestPath: request.Path.Value,
                    HttpMethod: request.Method,
                    UserId: context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    UserName: context.HttpContext.User.Identity?.IsAuthenticated == true
                        ? context.HttpContext.User.Identity.Name
                        : null,
                    ClientIp: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Browser: request.Headers.UserAgent.ToString(),
                    Category: ToCategory(failure.Kind),
                    StatusCode: StatusCodeFor(failure.Kind),
                    SafeMessage: failure.Message),
                CancellationToken.None);

            envelope["errorCode"] = log.ErrorCode;
            envelope["errorType"] = failure.Kind.ToString();
            envelope["correlationId"] = log.ErrorCode;
            envelope["statusCode"] = log.StatusCode;
            envelope["systemMessage"] = SystemErrorMessages.CreateForFailure(context.HttpContext, log.ErrorCode, failure.Kind);
            context.HttpContext.Items[LogErrorSystemMiddleware.LoggedItemKey] = log.ErrorCode;
            _logger.LogWarning("Persisted API failure {ErrorCode} for {Path}", log.ErrorCode, request.Path.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not persist API failure log for {Path}", request.Path.Value);
        }
    }

    private static IActionResult CreateJsonResult(IActionResult originalResult, JsonObject envelope)
    {
        var statusCode = originalResult switch
        {
            JsonResult jsonResult => jsonResult.StatusCode,
            ObjectResult objectResult => objectResult.StatusCode,
            IStatusCodeActionResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null
        };

        return new JsonResult(envelope)
        {
            StatusCode = statusCode
        };
    }

    private static bool TryGetJsonValue(IActionResult result, out object? value)
    {
        value = result switch
        {
            JsonResult jsonResult => jsonResult.Value,
            ObjectResult objectResult => objectResult.Value,
            _ => null
        };

        return value != null;
    }

    private static bool TryCreateFailureEnvelope(object value, out JsonObject envelope)
    {
        envelope = new JsonObject();
        try
        {
            var node = value as JsonNode ?? JsonSerializer.SerializeToNode(value, JsonOptions);
            if (node is not JsonObject obj) return false;

            if (!obj.TryGetPropertyValue("success", out var successNode)
                || successNode is not JsonValue successValue
                || !successValue.TryGetValue<bool>(out var success)
                || success)
            {
                return false;
            }

            envelope = JsonNode.Parse(obj.ToJsonString(JsonOptions))?.AsObject() ?? new JsonObject();
            return true;
        }
        catch
        {
            envelope = new JsonObject();
            return false;
        }
    }

    private static bool TryCreateStatusCodeFailureEnvelope(IActionResult result, out JsonObject envelope)
    {
        envelope = new JsonObject();
        var statusCode = result switch
        {
            ObjectResult objectResult => objectResult.StatusCode,
            JsonResult jsonResult => jsonResult.StatusCode,
            IStatusCodeActionResult statusCodeResult => statusCodeResult.StatusCode,
            _ => null
        };

        if (statusCode is not >= 400) return false;

        envelope["success"] = false;
        envelope["message"] = StatusCodeMessage(statusCode.Value);
        envelope["statusCode"] = statusCode.Value;

        if (TryGetJsonValue(result, out var value) && value != null)
        {
            try
            {
                envelope["details"] = JsonSerializer.SerializeToNode(value, JsonOptions);
            }
            catch
            {
                envelope["details"] = value.GetType().Name;
            }
        }

        return true;
    }

    private static bool HasErrorCode(JsonObject envelope)
    {
        var errorCode = ReadString(envelope, "errorCode");
        if (!string.IsNullOrWhiteSpace(errorCode)) return true;

        var correlationId = ReadString(envelope, "correlationId");
        if (string.IsNullOrWhiteSpace(correlationId)) return false;

        envelope["errorCode"] = correlationId;
        return true;
    }

    private static bool TryAttachEmbeddedErrorCode(ResultExecutingContext context, JsonObject envelope)
    {
        var message = ReadString(envelope, "message") ?? string.Empty;
        var match = ErrorCodePattern.Match(message);
        if (!match.Success) return false;

        envelope["errorCode"] = match.Value;
        envelope["errorType"] = SystemErrorKind.System.ToString();
        envelope["systemMessage"] = message;
        return true;
    }

    private static ApiFailure ExtractFailure(JsonObject envelope)
    {
        var message = ReadString(envelope, "message") ?? "Request failed.";
        var errors = ReadErrors(envelope);
        var kind = ParseErrorType(ReadString(envelope, "errorType"))
            ?? (errors.Count > 0 ? SystemErrorKind.Validation : SystemErrorKind.OperationFailure);

        return new ApiFailure(kind, errors.Count > 0 ? string.Join("; ", errors) : message);
    }

    private static SystemErrorKind? ParseErrorType(string? errorType)
    {
        if (string.IsNullOrWhiteSpace(errorType)) return null;
        return errorType.Trim() switch
        {
            "BusinessValidation" => SystemErrorKind.Validation,
            "BusinessDependency" => SystemErrorKind.BusinessDependency,
            "Timeout" => SystemErrorKind.Timeout,
            "Deadlock" => SystemErrorKind.Deadlock,
            "Unauthorized" => SystemErrorKind.Unauthorized,
            "Forbidden" => SystemErrorKind.Forbidden,
            "NotFound" => SystemErrorKind.NotFound,
            "DbUpdateException" => SystemErrorKind.DbUpdateException,
            _ => null
        };
    }

    private static SystemErrorCategory ToCategory(SystemErrorKind kind)
        => kind switch
        {
            SystemErrorKind.Validation => SystemErrorCategory.BusinessValidation,
            SystemErrorKind.BusinessDependency => SystemErrorCategory.BusinessDependency,
            SystemErrorKind.Timeout => SystemErrorCategory.Timeout,
            SystemErrorKind.Deadlock => SystemErrorCategory.Deadlock,
            SystemErrorKind.Unauthorized => SystemErrorCategory.Unauthorized,
            SystemErrorKind.Forbidden => SystemErrorCategory.Forbidden,
            SystemErrorKind.NotFound => SystemErrorCategory.NotFound,
            SystemErrorKind.DbUpdateException => SystemErrorCategory.DbUpdateException,
            _ => SystemErrorCategory.UnhandledException
        };

    private static int StatusCodeFor(SystemErrorKind kind)
        => kind switch
        {
            SystemErrorKind.Validation => StatusCodes.Status400BadRequest,
            SystemErrorKind.BusinessDependency => StatusCodes.Status409Conflict,
            SystemErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
            SystemErrorKind.Deadlock => StatusCodes.Status409Conflict,
            SystemErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            SystemErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            SystemErrorKind.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string? ReadString(JsonObject envelope, string propertyName)
    {
        if (!envelope.TryGetPropertyValue(propertyName, out var node) || node == null) return null;
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : node.ToJsonString(JsonOptions);
    }

    private static List<string> ReadErrors(JsonObject envelope)
    {
        var errors = new List<string>();
        if (!envelope.TryGetPropertyValue("errors", out var node) || node is not JsonArray array) return errors;

        foreach (var item in array)
        {
            if (item == null) continue;
            if (item is JsonValue value && value.TryGetValue<string>(out var text))
            {
                errors.Add(text);
            }
            else
            {
                errors.Add(item.ToJsonString(JsonOptions));
            }
        }

        return errors;
    }

    private static string StatusCodeMessage(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad request.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "Access denied for current role.",
            StatusCodes.Status404NotFound => "Requested data was not found.",
            StatusCodes.Status408RequestTimeout => "Request timeout.",
            _ when statusCode >= 500 => "Server error.",
            _ => "Request failed."
        };

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!ShouldCaptureBody(request) || !request.Body.CanSeek) return null;

        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(body)) return null;
        return body.Length <= MaxPayloadLength ? body : body[..MaxPayloadLength];
    }

    private static bool ShouldCaptureBody(HttpRequest request)
        => request.ContentLength is > 0
           && request.Method is not "GET" and not "HEAD"
           && (request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
               || request.ContentType?.Contains("form", StringComparison.OrdinalIgnoreCase) == true
               || request.ContentType?.Contains("text", StringComparison.OrdinalIgnoreCase) == true);

    private sealed record ApiFailure(SystemErrorKind Kind, string Message);
}
