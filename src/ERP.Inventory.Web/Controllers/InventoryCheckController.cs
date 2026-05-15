using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

/// <summary>
/// Kiểm kê kho theo phiên (session-based):
///   POST CreateSession  — Tạo phiếu CHK, SessionStatus = InProgress
///   POST Scan           — Gửi batch scan (có thể gọi nhiều lần)
///   POST Finalize/{id}  — Tính Missing, đánh dấu Finalized
///   GET  Progress/{id}  — Xem tiến độ / thống kê phiên hiện tại
/// </summary>
[Route("[controller]")]
[Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
public sealed class InventoryCheckController : Controller
{
    private readonly InventoryCheckService _inventoryCheckService;
    private readonly ICurrentUserService _currentUserService;

    public InventoryCheckController(InventoryCheckService inventoryCheckService, ICurrentUserService currentUserService)
    {
        _inventoryCheckService = inventoryCheckService;
        _currentUserService = currentUserService;
    }

    /// <summary>Tạo phiên kiểm kê mới (InProgress). Trả về DocumentId để dùng cho Scan/Finalize.</summary>
    [HttpPost("CreateSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession([FromBody] InventoryCheckSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryCheckService.CreateSessionAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    /// <summary>
    /// Gửi một batch scan vào phiên kiểm kê đang InProgress.
    /// Xử lý Matched / WrongLocation / Extra. Bỏ qua items đã scan trước đó trong cùng phiên.
    /// </summary>
    [HttpPost("Scan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan([FromBody] InventoryCheckScanRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryCheckService.ScanBatchAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    /// <summary>
    /// Finalize phiên kiểm kê: tính Missing, cập nhật status items → Lost.
    /// Chỉ gọi khi đã scan xong toàn bộ khu vực cần kiểm.
    /// </summary>
    [HttpPost("Finalize/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalize(int id, CancellationToken cancellationToken)
    {
        var result = await _inventoryCheckService.FinalizeAsync(id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    /// <summary>Xem tiến độ scan của phiên kiểm kê: tổng hợp theo Result type.</summary>
    [HttpGet("Progress/{id:int}")]
    public async Task<IActionResult> Progress(int id, CancellationToken cancellationToken)
    {
        var result = await _inventoryCheckService.GetSessionProgressAsync(id, cancellationToken);
        return result == null ? NotFound() : Json(result);
    }
}
