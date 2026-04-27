using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize]
public sealed class InventoryController : Controller
{
    private readonly ITrackingService _trackingService;
    private readonly IInboundService _inboundService;
    private readonly IInventoryOperationService _inventoryOperationService;
    private readonly ICurrentUserService _currentUserService;

    public InventoryController(ITrackingService trackingService, IInboundService inboundService, IInventoryOperationService inventoryOperationService, ICurrentUserService currentUserService)
    {
        _trackingService = trackingService;
        _inboundService = inboundService;
        _inventoryOperationService = inventoryOperationService;
        _currentUserService = currentUserService;
    }

    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] string? keyword, [FromQuery] int? warehouseId, [FromQuery] int? categoryId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _trackingService.GetInventoryListAsync(keyword, warehouseId, categoryId, status, page, pageSize, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Inbound")]
    [Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inbound([FromBody] InboundRequest request, CancellationToken cancellationToken)
    {
        var result = await _inboundService.CreateInboundAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("MoveLocation")]
    [Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLocation([FromBody] MoveLocationRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryOperationService.MoveLocationAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Adjust")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust([FromBody] AdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryOperationService.AdjustAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("InventoryCheck")]
    [Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InventoryCheck([FromBody] InventoryCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryOperationService.CreateInventoryCheckAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }
}
