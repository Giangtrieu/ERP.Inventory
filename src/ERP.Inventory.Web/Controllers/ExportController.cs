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

    // ─── Phase 6: New Export Endpoints ─────────────────────────────────────

    [HttpGet("QuantityBalance")]
    public async Task<IActionResult> QuantityBalance([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportQuantityBalanceAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "quantity-balance-export.xlsx");
    }

    [HttpGet("InboundDocuments")]
    public async Task<IActionResult> InboundDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportInboundDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "inbound-documents-export.xlsx");
    }

    [HttpGet("BorrowDocuments")]
    public async Task<IActionResult> BorrowDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportBorrowDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "borrow-documents-export.xlsx");
    }

    [HttpGet("RepairDocuments")]
    public async Task<IActionResult> RepairDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportRepairDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "repair-documents-export.xlsx");
    }

    [HttpGet("MoveDocuments")]
    public async Task<IActionResult> MoveDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportMoveDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "move-documents-export.xlsx");
    }

    [HttpGet("AdjustmentDocuments")]
    public async Task<IActionResult> AdjustmentDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportAdjustmentDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "adjustment-documents-export.xlsx");
    }

    [HttpGet("InventoryCheckDocuments")]
    public async Task<IActionResult> InventoryCheckDocuments([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportInventoryCheckDocumentsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "inventory-check-export.xlsx");
    }

    [HttpGet("QuantityTransactions")]
    public async Task<IActionResult> QuantityTransactions([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportQuantityTransactionsAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "quantity-transactions-export.xlsx");
    }

    [HttpGet("ItemMaster")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> ItemMaster([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportItemMasterAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "item-master-export.xlsx");
    }

    [HttpGet("WarehouseStructure")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> WarehouseStructure([FromQuery] ExportFilterDto filter, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportWarehouseStructureAsync(filter, _currentUserService.GetCurrentUser(), cancellationToken);
        return Excel(bytes, "warehouse-structure-export.xlsx");
    }

    private FileContentResult Excel(byte[] bytes, string fileName)
    {
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
