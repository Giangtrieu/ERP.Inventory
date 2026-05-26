using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace ERP.Inventory.Web.Controllers;

/// <summary>
/// Backward-compatible facade: delegates all endpoints to the new split controllers.
/// The SPA frontend still calls /Management/* so this keeps routes intact.
/// </summary>
[Authorize]
[Route("[controller]")]
public sealed class ManagementController : ManagementBaseController
{
    private readonly MasterDataController _masterData;
    private readonly WarehouseStructureController _warehouseStructure;
    private readonly UserManagementController _userManagement;
    private readonly SystemController _system;

    public ManagementController(InventoryDbContext db, ICurrentUserService currentUserService)
        : base(db, currentUserService)
    {
        _masterData = new MasterDataController(db, currentUserService);
        _warehouseStructure = new WarehouseStructureController(db, currentUserService);
        _userManagement = new UserManagementController(db, currentUserService);
        _system = new SystemController(db, currentUserService);
    }

    // ─── Master Data ──────────────────────────────────────────
    [HttpGet("MasterDataSummary")]
    public Task<IActionResult> MasterDataSummary(CancellationToken ct) => _masterData.MasterDataSummary(ct);

    [HttpGet("MasterDataList")]
    public Task<IActionResult> MasterDataList([FromQuery] string entity = "items", [FromQuery] string? keyword = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] bool? isActive = null, CancellationToken ct = default) => _masterData.MasterDataList(entity, keyword, page, pageSize, isActive, ct);

    [HttpPost("Category")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> CreateCategory([FromBody] MasterDataController.CategoryRequest request, CancellationToken ct) => _masterData.CreateCategory(request, ct);

    [HttpGet("Category/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")]
    public Task<IActionResult> Category(int id, CancellationToken ct) => _masterData.Category(id, ct);

    [HttpPut("Category/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateCategory(int id, [FromBody] MasterDataController.CategoryRequest request, CancellationToken ct) => _masterData.UpdateCategory(id, request, ct);

    [HttpPost("Category/{id:int}/Deactivate")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateCategory(int id, CancellationToken ct) => _masterData.DeactivateCategory(id, ct);

    [HttpPost("Category/{id:int}/Restore")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreCategory(int id, CancellationToken ct) => _masterData.RestoreCategory(id, ct);

    [HttpDelete("Category/{id:int}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteCategory(int id, CancellationToken ct) => _masterData.DeleteCategory(id, ct);

    [HttpPost("ExternalParty")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> CreateExternalParty([FromBody] MasterDataController.ExternalPartyRequest request, CancellationToken ct) => _masterData.CreateExternalParty(request, ct);

    [HttpGet("ExternalParty/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")]
    public Task<IActionResult> ExternalParty(int id, CancellationToken ct) => _masterData.ExternalParty(id, ct);

    [HttpPut("ExternalParty/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateExternalParty(int id, [FromBody] MasterDataController.ExternalPartyRequest request, CancellationToken ct) => _masterData.UpdateExternalParty(id, request, ct);

    [HttpPost("ExternalParty/{id:int}/Deactivate")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateExternalParty(int id, CancellationToken ct) => _masterData.DeactivateExternalParty(id, ct);

    [HttpPost("ExternalParty/{id:int}/Restore")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreExternalParty(int id, CancellationToken ct) => _masterData.RestoreExternalParty(id, ct);

    [HttpDelete("ExternalParty/{id:int}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteExternalParty(int id, CancellationToken ct) => _masterData.DeleteExternalParty(id, ct);

    [HttpPost("Item")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> CreateItem([FromBody] MasterDataController.ItemRequest request, CancellationToken ct) => _masterData.CreateItem(request, ct);

    [HttpGet("Item/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")]
    public Task<IActionResult> Item(int id, CancellationToken ct) => _masterData.Item(id, ct);

    [HttpPut("Item/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateItem(int id, [FromBody] MasterDataController.ItemRequest request, CancellationToken ct) => _masterData.UpdateItem(id, request, ct);

    [HttpPost("Item/{id:int}/Deactivate")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateItem(int id, CancellationToken ct) => _masterData.DeactivateItem(id, ct);

    [HttpPost("Item/{id:int}/Restore")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreItem(int id, CancellationToken ct) => _masterData.RestoreItem(id, ct);

    [HttpDelete("Item/{id:int}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteItem(int id, CancellationToken ct) => _masterData.DeleteItem(id, ct);

    [HttpGet("ItemInstance/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> ItemInstance(int id, CancellationToken ct)
    {
        var user = CurrentUserService.GetCurrentUser();
        var row = await Db.ItemInstances
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ItemId,
                ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                ItemName = x.Item != null ? x.Item.DefaultName : string.Empty,
                x.SerialNumber,
                x.MT,
                x.Barcode,
                x.OwnerName,
                Status = x.Status.ToString(),
                CurrentLocation = Db.CurrentItemLocations
                    .Where(l => l.ItemInstanceId == x.Id)
                    .Select(l => l.BinLocation != null
                        ? l.BinLocation.FullPath
                        : !string.IsNullOrWhiteSpace(l.ExternalLocationText)
                            ? (l.ExternalParty != null ? l.ExternalParty.Name + " - " + l.ExternalLocationText : l.ExternalLocationText)
                            : (l.ExternalParty != null ? l.ExternalParty.Name : (l.Warehouse != null ? l.Warehouse.Name : "Unknown")))
                    .FirstOrDefault(),
                WarehouseId = Db.CurrentItemLocations
                    .Where(l => l.ItemInstanceId == x.Id)
                    .Select(l => l.WarehouseId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row == null) return NotFound();
        if (row.WarehouseId.HasValue && !user.CanAccessWarehouse(row.WarehouseId.Value)) return Forbid();
        return Json(row);
    }

    [HttpPut("ItemInstanceUpdate/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItemInstance(int id, [FromBody] ItemInstanceRequest request, CancellationToken ct)
    {
        var validation = ValidateRequired(("ItemId", request.ItemId?.ToString()));
        if (validation != null) return validation;

        var user = CurrentUserService.GetCurrentUser();
        var entity = await Db.ItemInstances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        var currentWarehouseId = await Db.CurrentItemLocations
            .Where(x => x.ItemInstanceId == id)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync(ct);
        if (currentWarehouseId.HasValue && !user.CanAccessWarehouse(currentWarehouseId.Value)) return Forbid();

        if (!request.ItemId.HasValue)
            return Json(new { success = false, message = "ItemId is required." });

        var itemId = request.ItemId.Value;
        if (!await Db.Items.AnyAsync(x => x.Id == itemId && x.IsActive, ct))
            return Json(new { success = false, message = "Item is invalid." });

        var serialNumber = NullIfWhiteSpace(request.SerialNumber);
        var barcode = NullIfWhiteSpace(request.Barcode);
        var mt = NullIfWhiteSpace(request.MT);
        var ownerName = NullIfWhiteSpace(request.OwnerName);

        if (serialNumber != null && await Db.ItemInstances.AnyAsync(x => x.Id != id && x.ItemId == itemId && x.SerialNumber == serialNumber, ct))
            return Json(new { success = false, message = $"Serial {serialNumber} already exists for item {itemId}." });

        if (barcode != null && await Db.ItemInstances.AnyAsync(x => x.Id != id && x.Barcode == barcode, ct))
            return Json(new { success = false, message = $"Barcode {barcode} already exists." });

        entity.ItemId = itemId;
        entity.SerialNumber = serialNumber;
        entity.MT = mt;
        entity.Barcode = barcode;
        entity.OwnerName = ownerName;

        Touch(entity);
        AddAudit("Update", nameof(ItemInstance), entity.Id, entity.SerialNumber ?? entity.Barcode ?? entity.Id.ToString());
        await Db.SaveChangesAsync(ct);
        return Json(new { success = true, data = new { entity.Id, entity.ItemId, entity.SerialNumber, entity.Barcode, entity.MT, entity.OwnerName } });
    }

    [HttpDelete("ItemInstanceDelete/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItemInstance(int id, CancellationToken ct)
    {
        if (id == 0)return Json(new { success = false, message = "Item instance not found." });

        var user = CurrentUserService.GetCurrentUser();
        var entity = await Db.ItemInstances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)return NotFound();

        var hasDelete =await Db.BorrowDocumentLines.AnyAsync(x => x.ItemInstanceId == id, ct) || await Db.RepairDocumentLines.AnyAsync(x => x.ItemInstanceId == id, ct);
        if (hasDelete)
        {
            return Json(new{ success = false,message = "Cannot hard delete this record because it is referenced by operational data. Use soft delete instead."});
        }

        var current = await Db.CurrentItemLocations .Where(x => x.ItemInstanceId == id).ToListAsync(ct);

        if (!current.Any())return NotFound();

        var history = await Db.ItemMovementHistories.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var itemTransaction = await Db.InventoryTransactions.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var inboundLog = await Db.InboundDocumentLogs.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var inboundLine = await Db.InboundDocumentLines .Where(x => x.ItemInstanceId == id) .ToListAsync(ct);
        var adjustLog = await Db.AdjustmentDocumentLogs.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var adjustLine = await Db.AdjustmentDocumentLines.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var moveLine = await Db.MoveDocumentLines.Where(x => x.ItemInstanceId == id).ToListAsync(ct);
        var inventoryCheck = await Db.InventoryCheckLines.Where(x => x.ItemInstanceId == id).ToListAsync(ct);

        await using var transaction = await Db.Database.BeginTransactionAsync(ct);
        try
        {
            if (inboundLog.Any()) Db.InboundDocumentLogs.RemoveRange(inboundLog);
            if (inboundLine.Any()) Db.InboundDocumentLines.RemoveRange(inboundLine);
            if (adjustLog.Any()) Db.AdjustmentDocumentLogs.RemoveRange(adjustLog);
            if (adjustLine.Any()) Db.AdjustmentDocumentLines.RemoveRange(adjustLine);
            if (moveLine.Any()) Db.MoveDocumentLines.RemoveRange(moveLine);
            if (inventoryCheck.Any()) Db.InventoryCheckLines.RemoveRange(inventoryCheck);
            if (itemTransaction.Any()) Db.InventoryTransactions.RemoveRange(itemTransaction);
            if (history.Any()) Db.ItemMovementHistories.RemoveRange(history);

            Db.CurrentItemLocations.RemoveRange(current);
            Db.ItemInstances.Remove(entity);
            AddAudit("HardDelete", nameof(ItemInstance), entity.Id, entity.SerialNumber ?? entity.Barcode ?? entity.Id.ToString());

            await Db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Json(new{ success = true, message = "Deleted successfully."});
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return Json(new { success = false,message = ex.Message});
        }
    }

    // ─── Warehouse Structure ──────────────────────────────────
    [HttpGet("WarehouseStructure")]
    public Task<IActionResult> WarehouseStructure([FromQuery] int? warehouseId, [FromQuery] bool? isActive, [FromQuery] string? keyword, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct) => _warehouseStructure.List(warehouseId, isActive, keyword, page, pageSize, ct);

    [HttpPost("WarehouseStructure")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> CreateWarehouseStructure([FromBody] WarehouseStructureController.WarehouseStructureRequest request, CancellationToken ct) => _warehouseStructure.Create(request, ct);

    [HttpGet("WarehouseStructure/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")]
    public Task<IActionResult> WarehouseStructureDetail(int id, CancellationToken ct) => _warehouseStructure.Detail(id, ct);

    [HttpPut("WarehouseStructure/{id:int}")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateWarehouseStructure(int id, [FromBody] WarehouseStructureController.WarehouseStructureRequest request, CancellationToken ct) => _warehouseStructure.Update(id, request, ct);

    [HttpPost("WarehouseStructure/{id:int}/Deactivate")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateWarehouseStructure(int id, CancellationToken ct) => _warehouseStructure.Deactivate(id, ct);

    [HttpPost("WarehouseStructure/{id:int}/Restore")][Authorize(Roles = "Admin,Warehouse Manager")][ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreWarehouseStructure(int id, CancellationToken ct) => _warehouseStructure.Restore(id, ct);

    [HttpDelete("WarehouseStructure/{id:int}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteWarehouseStructure(int id, CancellationToken ct) => _warehouseStructure.Delete(id, ct);

    // ─── Users ────────────────────────────────────────────────
    [HttpGet("Roles")][Authorize(Roles = "Admin")]
    public Task<IActionResult> Roles(CancellationToken ct) => _userManagement.Roles(ct);

    [HttpGet("Users")][Authorize(Roles = "Admin")]
    public Task<IActionResult> Users([FromQuery] string? keyword = null, [FromQuery] bool? isActive = null, CancellationToken ct = default) => _userManagement.Users(keyword, isActive, ct);

    [HttpGet("User/{id}")][Authorize(Roles = "Admin")]
    public Task<IActionResult> UserDetail(string id, CancellationToken ct) => _userManagement.UserDetail(id, ct);

    [HttpPost("User")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> CreateUser([FromBody] UserManagementController.UserRequest request, CancellationToken ct) => _userManagement.CreateUser(request, ct);

    [HttpPut("User/{id}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateUser(string id, [FromBody] UserManagementController.UserRequest request, CancellationToken ct) => _userManagement.UpdateUser(id, request, ct);

    [HttpPost("User/{id}/Deactivate")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateUser(string id, CancellationToken ct) => _userManagement.DeactivateUser(id, ct);

    [HttpPost("User/{id}/Restore")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreUser(string id, CancellationToken ct) => _userManagement.RestoreUser(id, ct);

    [HttpDelete("User/{id}")][Authorize(Roles = "Admin")][ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteUser(string id, CancellationToken ct) => _userManagement.DeleteUser(id, ct);

    // ─── System ───────────────────────────────────────────────
    [HttpGet("SystemSummary")][Authorize(Roles = "Admin,Warehouse Manager")]
    public Task<IActionResult> SystemSummary(CancellationToken ct) => _system.SystemSummary(ct);

    [HttpGet("AuditLogs")]
    public Task<IActionResult> AuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] string? keyword = null, [FromQuery] string? userName = null, [FromQuery] string? action = null, [FromQuery] string? entityName = null, [FromQuery] string? referenceNo = null, CancellationToken ct = default) => _system.AuditLogs(page, pageSize, fromDate, toDate, keyword, userName, action, entityName, referenceNo, ct);

    [HttpGet("ImportBatches")]
    public Task<IActionResult> ImportBatches(CancellationToken ct) => _system.ImportBatches(ct);

    public sealed class ItemInstanceRequest
    {
        public int? ItemId { get; init; }
        public string? SerialNumber { get; init; }
        public string? MT { get; init; }
        public string? Barcode { get; init; }
        public string? OwnerName { get; init; }
    }
}
