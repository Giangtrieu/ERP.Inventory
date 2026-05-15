using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

/// <summary>
/// Reconciliation Audit — Đối soát tài sản.
/// Roles: Admin, Warehouse Manager.
/// </summary>
[Route("[controller]")]
[Authorize(Roles = "Admin,Warehouse Manager, Warehouse Staff")]
public sealed class ReconciliationController : Controller
{
    private readonly ReconciliationService _service;
    private readonly ICurrentUserService _currentUserService;

    public ReconciliationController(ReconciliationService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    // ─── Reference Lists ──────────────────────────────────────────────────────

    /// <summary>Lấy danh sách tất cả Reference Lists mà user có quyền.</summary>
    [HttpGet("Lists")]
    public async Task<IActionResult> GetLists(CancellationToken ct)
    {
        var result = await _service.GetReferenceListsAsync(_currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>Tạo Reference List mới.</summary>
    [HttpPost("CreateList")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateList([FromBody] CreateReferenceListRequest request, CancellationToken ct)
    {
        var result = await _service.CreateReferenceListAsync(request, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>Lấy danh sách items của 1 Reference List.</summary>
    [HttpGet("ListItems/{id:int}")]
    public async Task<IActionResult> GetListItems(int id, CancellationToken ct)
    {
        var result = await _service.GetReferenceListItemsAsync(id, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>
    /// Import Excel vào Reference List.
    /// Form: file (IFormFile) + listId (int) + importMode (Supplement|Replace).
    /// </summary>
    [HttpPost("ImportList")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportList(
        [FromForm] int listId,
        [FromForm] string importMode,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "No file uploaded." });

        if (!Enum.TryParse<ReferenceListImportMode>(importMode, true, out var mode))
            mode = ReferenceListImportMode.Supplement;

        await using var stream = file.OpenReadStream();
        var result = await _service.ImportReferenceListFromStreamAsync(
            listId, mode, stream, file.FileName, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    // ─── Sessions ─────────────────────────────────────────────────────────────

    /// <summary>Lấy tất cả phiên đối soát (trừ Archived).</summary>
    [HttpGet("Sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var result = await _service.GetSessionsAsync(_currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>Tạo phiên đối soát mới (Draft).</summary>
    [HttpPost("CreateSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession([FromBody] CreateReconciliationSessionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateSessionAsync(request, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>
    /// Chạy so sánh cho phiên đối soát.
    /// Algorithm O(n) HashSet — kết quả ghi vào ReconciliationResults.
    /// </summary>
    [HttpPost("RunSession/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunSession(int id, CancellationToken ct)
    {
        var result = await _service.RunSessionAsync(id, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>Xem kết quả chi tiết của 1 phiên đối soát (có filter + phân trang).</summary>
    [HttpGet("SessionResults/{id:int}")]
    public async Task<IActionResult> GetSessionResults(int id, [FromQuery] string? resultType,[FromQuery] string? keyword,[FromQuery] int page = 1,[FromQuery] int pageSize = 50,CancellationToken ct = default)
    {
        var filter = new ReconciliationResultFilter
        {
            ResultType = resultType,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };
        var result = await _service.GetSessionResultsAsync(id, filter, _currentUserService.GetCurrentUser(), ct);
        return Json(result);
    }

    /// <summary>Xuất kết quả phiên đối soát ra Excel.</summary>
    [HttpGet("ExportSession/{id:int}")]
    public async Task<IActionResult> ExportSession(int id, [FromQuery] string? resultType, [FromQuery] string? keyword, CancellationToken ct)
    {
        var user = _currentUserService.GetCurrentUser();
        var filter = new ReconciliationResultFilter
        {
            ResultType = resultType,
            Keyword = keyword,
        };
        var bytes = await _service.ExportSessionResultAsync(id, filter, user, ct);
        if (bytes.Length == 0) return NotFound();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reconciliation_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("Template")]
    public IActionResult Template()
    {
        var user = _currentUserService.GetCurrentUser();
        var bytes = _service.GetReferenceListTemplate(user.LanguageCode ?? "vi");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ReferenceListTemplate.xlsx");
    }
}
