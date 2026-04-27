using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class ExportController : Controller
{
    private readonly IExportService _exportService;
    private readonly ICurrentUserService _currentUserService;

    public ExportController(IExportService exportService, ICurrentUserService currentUserService)
    {
        _exportService = exportService;
        _currentUserService = currentUserService;
    }

    [HttpGet("Inventory")]
    public async Task<IActionResult> Inventory([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportInventoryAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "inventory-export.xlsx");
    }

    [HttpGet("History")]
    public async Task<IActionResult> History([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportHistoryAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "history-export.xlsx");
    }

    [HttpGet("Audit")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Audit([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportAuditAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "audit-export.xlsx");
    }

    private FileContentResult Excel(byte[] bytes, string fileName)
    {
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
