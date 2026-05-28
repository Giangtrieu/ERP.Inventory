using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff, Viewer")]
[Route("[controller]")]
public sealed class ImportController : Controller
{
    private readonly IImportService _importService;
    private readonly ICurrentUserService _currentUserService;

    public ImportController(IImportService importService, ICurrentUserService currentUserService)
    {
        _importService = importService;
        _currentUserService = currentUserService;
    }

    [HttpGet("Types")]
    public IActionResult Types()
    {
        var language = _currentUserService.GetCurrentUser().LanguageCode;
        return Json(new[]
        {
            new { id = "ItemMaster",        text = LocalizationCatalog.Text(language, "ImportType.ItemMaster") },
            new { id = "WarehouseStructure",text = LocalizationCatalog.Text(language, "ImportType.WarehouseStructure") },
            new { id = "Inbound",           text = LocalizationCatalog.Text(language, "ImportType.Inbound") },
            new { id = "InventoryCheck",    text = LocalizationCatalog.Text(language, "ImportType.InventoryCheck") },
            new { id = "RepairSend",        text = LocalizationCatalog.Text(language, "ImportType.RepairSend") },
            new { id = "BorrowLend",        text = LocalizationCatalog.Text(language, "ImportType.BorrowLend") },
            new { id = "QuantityInbound",   text = LocalizationCatalog.Text(language, "ImportType.QuantityInbound") },
            new { id = "QuantityOutbound",  text = LocalizationCatalog.Text(language, "ImportType.QuantityOutbound") },
            new { id = "QuantityAdjust",    text = LocalizationCatalog.Text(language, "ImportType.QuantityAdjust") },
            new { id = "MoveLocation",      text = LocalizationCatalog.Text(language, "ImportType.MoveLocation") },
            new { id = "BorrowReturn",      text = LocalizationCatalog.Text(language, "ImportType.BorrowReturn") },
            new { id = "RepairReceive",     text = LocalizationCatalog.Text(language, "ImportType.RepairReceive") },
        });
    }

    [HttpGet("Template")]
    public async Task<IActionResult> Template([FromQuery] string importType, CancellationToken cancellationToken)
    {
        if (!CanUseImportType(importType))
        {
            return Forbid();
        }

        var bytes = await _importService.TemplateAsync(importType, _currentUserService.GetCurrentUser(), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{importType}-template.xlsx");
    }

    [HttpGet("Batches")]
    public async Task<IActionResult> Batches(CancellationToken cancellationToken)
    {
        var result = await _importService.ListAsync(_currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("Rows/{id:int}")]
    public async Task<IActionResult> Rows(int id, CancellationToken cancellationToken)
    {
        var result = await _importService.RowsAsync(id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] string importType, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (!CanUseImportType(importType))
        {
            return Json(new { success = false, message = "Current role cannot use this import type." });
        }

        if (file.Length == 0)
        {
            return Json(new { success = false, message = "File is empty." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _importService.UploadAsync(importType, file.FileName, stream, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Validate/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateBatch(int id, CancellationToken cancellationToken)
    {
        var result = await _importService.ValidateAsync(id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Confirm/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        var result = await _importService.ConfirmAsync(id, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    private bool CanUseImportType(string importType)
    {
        var user = _currentUserService.GetCurrentUser();
        var normalized = importType.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized is not ("ItemMaster" or "WarehouseStructure") || user.CanManage;
    }
}
