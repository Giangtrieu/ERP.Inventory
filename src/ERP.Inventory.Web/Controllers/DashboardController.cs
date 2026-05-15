using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize]
public sealed class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUserService _currentUserService;

    public DashboardController(IDashboardService dashboardService, ICurrentUserService currentUserService)
    {
        _dashboardService = dashboardService;
        _currentUserService = currentUserService;
    }

    [HttpGet("Summary")]
    public async Task<IActionResult> Summary([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetSummaryAsync(warehouseId, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("StockByWarehouse")]
    public async Task<IActionResult> StockByWarehouse([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetStockByWarehouseAsync(status, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("StockByStatus")]
    public async Task<IActionResult> StockByStatus([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetStockByStatusAsync(warehouseId, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("MovementTrend")]
    public async Task<IActionResult> MovementTrend([FromQuery] int? warehouseId, [FromQuery] int days = 14, CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetMovementTrendAsync(warehouseId, days, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("MovementByAction")]
    public async Task<IActionResult> MovementByAction([FromQuery] int? warehouseId, [FromQuery] int days = 14, CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetMovementByActionAsync(warehouseId, days, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("StockByCategory")]
    public async Task<IActionResult> StockByCategory([FromQuery] int? warehouseId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetStockByCategoryAsync(warehouseId, status, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("LocationUtilization")]
    public async Task<IActionResult> LocationUtilization([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetLocationUtilizationAsync(warehouseId, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("OverdueBorrowAging")]
    public async Task<IActionResult> OverdueBorrowAging([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetOverdueBorrowAgingAsync(warehouseId, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("QuantitySummary")]
    public async Task<IActionResult> QuantitySummary([FromQuery] int? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetQuantitySummaryAsync(warehouseId, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(new { success = true, data = result });
    }
}
