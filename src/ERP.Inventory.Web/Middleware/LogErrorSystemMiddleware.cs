using ERP.Inventory.Infrastructure.Services;
using ERP.Inventory.Web.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ERP.Inventory.Web.Middleware;

public sealed class LogErrorSystemMiddleware
{
    private const int MaxPayloadLength = 16000;
    private readonly RequestDelegate _next;
    private readonly ILogger<LogErrorSystemMiddleware> _logger;

    public LogErrorSystemMiddleware(RequestDelegate next, ILogger<LogErrorSystemMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ILogErrorSystemService errorLog)
    {
        string? payloadJson = null;

        if (ShouldCaptureBody(context.Request))
        {
            payloadJson = await ReadRequestBodyAsync(context.Request);
        }

        try
        {
            await _next(context);
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
                Browser: context.Request.Headers.UserAgent.ToString());

            var log = await errorLog.LogAsync(ex, logContext, context.RequestAborted);
            _logger.LogError(ex, "Persisted system exception {ErrorCode}", log.ErrorCode);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                var message = LocalizedSystemError(context, log.ErrorCode);
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    errorCode = log.ErrorCode,
                    message
                }), Encoding.UTF8);
            }
        }
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

    private static string LocalizedSystemError(HttpContext context, string errorCode)
    {
        var language = context.User.FindFirstValue("language")
                       ?? context.Request.Query["lang"].FirstOrDefault()
                       ?? "vi";
        return string.Format(LocalizationCatalog.Text(language, "SystemError.UserMessage"), errorCode);
    }
}
