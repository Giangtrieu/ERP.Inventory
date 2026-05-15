using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize]
public sealed class QuantityInventoryController : Controller
{
    private readonly IQuantityInventoryService _service;
    private readonly ICurrentUserService _currentUserService;

    public QuantityInventoryController(IQuantityInventoryService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    [HttpGet("Balances")]
    public async Task<IActionResult> Balances([FromQuery] string? keyword, [FromQuery] int? warehouseId, [FromQuery] int? itemId, [FromQuery] string? status, [FromQuery] string? ownerName, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetBalancesAsync(keyword, warehouseId, itemId, status, ownerName, page, pageSize, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(new { success = true, data = result });
    }

    [HttpGet("Transactions")]
    public async Task<IActionResult> Transactions([FromQuery] string? keyword, [FromQuery] int? warehouseId, [FromQuery] int? itemId, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetTransactionsAsync(keyword, warehouseId, itemId, take, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(new { success = true, data = result });
    }

    [HttpPost("Receive")]
    [Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive([FromBody] QuantityInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ReceiveAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Issue")]
    [Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue([FromBody] QuantityInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.IssueAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Adjust")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust([FromBody] QuantityInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.AdjustAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("Instances")]
    public async Task<IActionResult> Instances([FromQuery] string? itemCode, [FromQuery] int? warehouseId, [FromQuery] string? ownerName, CancellationToken cancellationToken)
    {
        var result = await _service.GetInstancesAsync(itemCode, warehouseId, ownerName, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(new { success = true, data = result });
    }
}
