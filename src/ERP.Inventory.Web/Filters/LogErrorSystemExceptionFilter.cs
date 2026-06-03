using ERP.Inventory.Infrastructure.Services;
using ERP.Inventory.Web.Services;
using ERP.Inventory.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;

namespace ERP.Inventory.Web.Filters;

public sealed class LogErrorSystemExceptionFilter : IAsyncExceptionFilter
{
    private const int MaxPayloadLength = 16000;
    private readonly ILogErrorSystemService _errorLog;
    private readonly ILogger<LogErrorSystemExceptionFilter> _logger;

    public LogErrorSystemExceptionFilter(
        ILogErrorSystemService errorLog,
        ILogger<LogErrorSystemExceptionFilter> logger)
    {
        _errorLog = errorLog;
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var request = context.HttpContext.Request;
        var route = context.RouteData.Values;
        var logContext = new LogErrorContext(
            Module: route.TryGetValue("controller", out var controller) ? controller?.ToString() : null,
            Action: route.TryGetValue("action", out var action) ? action?.ToString() : null,
            PayloadJson: await ReadRequestBodyAsync(request),
            RequestPath: request.Path.Value,
            HttpMethod: request.Method,
            UserId: context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName: context.HttpContext.User.Identity?.IsAuthenticated == true
                ? context.HttpContext.User.Identity.Name
                : null,
            ClientIp: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            Browser: request.Headers.UserAgent.ToString());

        var log = await _errorLog.LogAsync(context.Exception, logContext, CancellationToken.None);
        _logger.LogError(context.Exception, "Persisted system exception {ErrorCode}", log.ErrorCode);

        var message = SystemErrorMessages.Create(context.HttpContext, log.ErrorCode, context.Exception);
        context.Result = CreateErrorResult(context, log.ErrorCode, message);
        context.ExceptionHandled = true;
    }

    private static IActionResult CreateErrorResult(ExceptionContext context, string errorCode, string message)
    {
        var request = context.HttpContext.Request;
        var controller = context.RouteData.Values.TryGetValue("controller", out var controllerValue)
            ? controllerValue?.ToString()
            : null;
        var action = context.RouteData.Values.TryGetValue("action", out var actionValue)
            ? actionValue?.ToString()
            : null;

        if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var form = request.HasFormContentType ? request.Form : null;
            var model = new LoginViewModel
            {
                UserName = form?["UserName"].FirstOrDefault() ?? string.Empty,
                ReturnUrl = form?["ReturnUrl"].FirstOrDefault(),
                LanguageCode = form?["LanguageCode"].FirstOrDefault() ?? "vi",
                RememberMe = string.Equals(form?["RememberMe"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase)
            };

            context.ModelState.AddModelError(string.Empty, message);
            return new ViewResult
            {
                ViewName = "Login",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<LoginViewModel>(
                    context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider>(),
                    context.ModelState)
                {
                    Model = model
                },
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        return new JsonResult(new
        {
            success = false,
            errorCode,
            message
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }

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
}
