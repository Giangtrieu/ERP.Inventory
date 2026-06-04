using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Services;
using ERP.Inventory.Web.Services;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ERP.Inventory.Web.Middleware;

public sealed class LogErrorSystemMiddleware
{
    private const int MaxPayloadLength = 16000;
    public const string LoggedItemKey = "__LogErrorSystemLogged";
    private readonly RequestDelegate _next;
    private readonly ILogger<LogErrorSystemMiddleware> _logger;

    public LogErrorSystemMiddleware(RequestDelegate next, ILogger<LogErrorSystemMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ILogErrorSystemService errorLog)
    {
        var startedAt = Stopwatch.GetTimestamp();
        string? payloadJson = null;

        if (ShouldCaptureBody(context.Request))
        {
            payloadJson = await ReadRequestBodyAsync(context.Request);
        }

        try
        {
            await _next(context);
            await LogStatusCodeFailureAsync(context, errorLog, payloadJson, ElapsedMs(startedAt));
        }
        catch (Exception ex)
        {
            var route = context.GetRouteData();
            var logContext = new LogErrorContext(
                Module: route.Values.TryGetValue("controller", out var controller) ? controller?.ToString() : null,
                Action: route.Values.TryGetValue("action", out var action) ? action?.ToString() : null,
                PayloadJson: payloadJson,
                RequestPath: context.Request.Path.Value,
                HttpMethod: context.Request.Method,
                UserId: context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName: context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null,
                ClientIp: context.Connection.RemoteIpAddress?.ToString(),
                Browser: context.Request.Headers.UserAgent.ToString(),
                StatusCode: StatusCodeFor(ex),
                DurationMs: ElapsedMs(startedAt));

            var log = await errorLog.LogAsync(ex, logContext, CancellationToken.None);
            context.Items[LoggedItemKey] = log.ErrorCode;
            _logger.LogError(ex, "Persisted system exception {ErrorCode}", log.ErrorCode);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = log.StatusCode ?? StatusCodeFor(ex);
                context.Response.ContentType = "application/json; charset=utf-8";
                var category = Enum.TryParse<SystemErrorCategory>(log.Category, out var parsed)
                    ? parsed
                    : SystemErrorCategory.UnhandledException;
                var message = SystemErrorMessages.Create(context, log.ErrorCode, category);
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    errorType = log.Category,
                    errorCode = log.ErrorCode,
                    correlationId = log.ErrorCode,
                    statusCode = context.Response.StatusCode,
                    message
                }), Encoding.UTF8);
            }
        }
    }

    private static async Task LogStatusCodeFailureAsync(HttpContext context, ILogErrorSystemService errorLog, string? payloadJson, long durationMs)
    {
        if (context.Items.ContainsKey(LoggedItemKey) || context.Response.StatusCode < 400)
        {
            return;
        }

        var status = context.Response.StatusCode;
        if (status is not (401 or 403 or 404 or 408 or >= 500))
        {
            return;
        }

        var route = context.GetRouteData();
        var category = SystemErrorClassifier.Classify(new InvalidOperationException($"HTTP {status} returned."), status);
        var log = await errorLog.LogAsync(new InvalidOperationException($"HTTP {status} returned for {context.Request.Method} {context.Request.Path}."),
            new LogErrorContext(
                Module: route.Values.TryGetValue("controller", out var controller) ? controller?.ToString() : null,
                Action: route.Values.TryGetValue("action", out var action) ? action?.ToString() : null,
                PayloadJson: payloadJson,
                RequestPath: context.Request.Path.Value,
                HttpMethod: context.Request.Method,
                UserId: context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName: context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null,
                ClientIp: context.Connection.RemoteIpAddress?.ToString(),
                Browser: context.Request.Headers.UserAgent.ToString(),
                Category: category,
                StatusCode: status,
                DurationMs: durationMs,
                SafeMessage: SystemErrorMessages.DefaultMessageKey(category)),
            CancellationToken.None);

        context.Items[LoggedItemKey] = log.ErrorCode;
    }

    private static bool ShouldCaptureBody(HttpRequest request)
        => request.ContentLength is > 0
           && request.Method is not "GET" and not "HEAD"
           && (request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
               || request.ContentType?.Contains("form", StringComparison.OrdinalIgnoreCase) == true
               || request.ContentType?.Contains("text", StringComparison.OrdinalIgnoreCase) == true);

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(body)) return null;
        return body.Length <= MaxPayloadLength ? body : body[..MaxPayloadLength];
    }

    private static long ElapsedMs(long startedAt)
        => (long)((Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency);

    private static int StatusCodeFor(Exception exception)
        => SystemErrorClassifier.Classify(exception) switch
        {
            SystemErrorCategory.Timeout => StatusCodes.Status504GatewayTimeout,
            SystemErrorCategory.Deadlock => StatusCodes.Status409Conflict,
            SystemErrorCategory.DbUpdateException => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

}
