using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
public sealed class RepairController : Controller
{
    private readonly IRepairService _repairService;
    private readonly ICurrentUserService _currentUserService;

    public RepairController(IRepairService repairService, ICurrentUserService currentUserService)
    {
        _repairService = repairService;
        _currentUserService = currentUserService;
    }

    [HttpPost("SendToRepair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendToRepair([FromBody] RepairSendRequest request, CancellationToken cancellationToken)
    {
        var result = await _repairService.SendToRepairAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("ReceiveFromRepair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveFromRepair([FromBody] RepairReceiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _repairService.ReceiveFromRepairAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }
}
