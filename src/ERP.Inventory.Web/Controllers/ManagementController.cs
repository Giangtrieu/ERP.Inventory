using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Common;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class ManagementController : Controller
{
    private readonly InventoryDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ManagementController(InventoryDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("WarehouseStructure")]
    public async Task<IActionResult> WarehouseStructure([FromQuery] int? warehouseId, [FromQuery] bool? isActive, [FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        var user = _currentUserService.GetCurrentUser();
        var query = _db.BinLocations.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Shelf)!.ThenInclude(x => x!.Rack)!.ThenInclude(x => x!.WarehouseZone)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.BinCode.Contains(key) || x.FullPath.Contains(key));
        }

        var rows = await query
            .OrderBy(x => x.FullPath)
            .Take(200)
            .Select(x => new
            {
                id = x.Id,
                warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode : string.Empty,
                zone = x.Shelf != null && x.Shelf.Rack != null && x.Shelf.Rack.WarehouseZone != null ? x.Shelf.Rack.WarehouseZone.ZoneCode : string.Empty,
                rack = x.Shelf != null && x.Shelf.Rack != null ? x.Shelf.Rack.RackCode : string.Empty,
                shelf = x.Shelf != null ? x.Shelf.ShelfCode : string.Empty,
                bin = x.BinCode,
                fullPath = x.FullPath,
                isActive = x.IsActive
            })
            .ToArrayAsync(cancellationToken);

        return Json(rows);
    }

    [HttpGet("MasterDataSummary")]
    public async Task<IActionResult> MasterDataSummary(CancellationToken cancellationToken)
    {
        var result = new
        {
            items = await _db.Items.CountAsync(cancellationToken),
            serialManaged = await _db.Items.CountAsync(x => x.IsSerialManaged, cancellationToken),
            categories = await _db.ItemCategories.CountAsync(cancellationToken),
            externalParties = await _db.ExternalParties.CountAsync(cancellationToken),
            translations = await _db.ItemTranslations.CountAsync(cancellationToken) + await _db.Translations.CountAsync(cancellationToken)
        };

        return Json(result);
    }

    [HttpGet("MasterDataList")]
    public async Task<IActionResult> MasterDataList([FromQuery] string entity = "items", [FromQuery] string? keyword = null, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var normalizedEntity = string.IsNullOrWhiteSpace(entity) ? "items" : entity.Trim().ToLowerInvariant();
        if (normalizedEntity == "categories")
        {
            var rows = await _db.ItemCategories.AsNoTracking()
                .Where(x => (!isActive.HasValue || x.IsActive == isActive.Value) && (key == null || x.CategoryCode.Contains(key) || x.Name.Contains(key)))
                .OrderBy(x => x.CategoryCode)
                .Take(100)
                .Select(x => new { id = x.Id, entity = "categories", code = x.CategoryCode, name = x.Name, type = "Category", serialTracking = "-", languageCoverage = "-", status = x.IsActive ? "Active" : "Inactive", isActive = x.IsActive })
                .ToArrayAsync(cancellationToken);
            return Json(rows);
        }

        if (normalizedEntity == "parties")
        {
            var rows = await _db.ExternalParties.AsNoTracking()
                .Where(x => (!isActive.HasValue || x.IsActive == isActive.Value) && (key == null || x.PartyCode.Contains(key) || x.Name.Contains(key)))
                .OrderBy(x => x.PartyCode)
                .Take(100)
                .Select(x => new { id = x.Id, entity = "parties", code = x.PartyCode, name = x.Name, type = x.PartyType.ToString(), serialTracking = "-", languageCoverage = "-", status = x.IsActive ? "Active" : "Inactive", isActive = x.IsActive })
                .ToArrayAsync(cancellationToken);
            return Json(rows);
        }

        var items = await _db.Items.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Translations)
                .Where(x => (!isActive.HasValue || x.IsActive == isActive.Value) && (key == null || x.ItemCode.Contains(key) || x.DefaultName.Contains(key)))
                .OrderBy(x => x.ItemCode)
                .Take(100)
                .ToArrayAsync(cancellationToken);
        return Json(items.Select(x => new
        {
            id = x.Id,
            entity = "items",
            code = x.ItemCode,
            name = x.DefaultName,
            type = x.Category != null ? $"Item / {x.Category.CategoryCode}" : "Item",
            serialTracking = x.IsSerialManaged ? "Yes" : "No",
            languageCoverage = string.Join(" / ", x.Translations.Select(t => t.LanguageCode).Distinct().OrderBy(t => t)),
            status = x.IsActive ? "Active" : "Inactive",
            isActive = x.IsActive
        }));
    }

    [HttpGet("AuditLogs")]
    public async Task<IActionResult> AuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] string? keyword = null, [FromQuery] string? userName = null, [FromQuery] string? action = null, [FromQuery] string? entityName = null, [FromQuery] string? referenceNo = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < to);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.UserName.Contains(key) || x.Action.Contains(key) || x.EntityName.Contains(key) || (x.ReferenceNo != null && x.ReferenceNo.Contains(key)));
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var key = userName.Trim();
            query = query.Where(x => x.UserName.Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var key = action.Trim();
            query = query.Where(x => x.Action.Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var key = entityName.Trim();
            query = query.Where(x => x.EntityName.Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(referenceNo))
        {
            var key = referenceNo.Trim();
            query = query.Where(x => x.ReferenceNo != null && x.ReferenceNo.Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.CreatedAt, x.UserName, x.Action, x.EntityName, x.ReferenceNo, x.Result })
            .ToArrayAsync(cancellationToken);

        return Json(new PagedResult<object> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("ImportBatches")]
    public async Task<IActionResult> ImportBatches(CancellationToken cancellationToken)
    {
        var rows = await _db.ImportBatchRows.AsNoTracking()
            .Include(x => x.ImportBatch)
            .Where(x => !x.IsValid || x.Severity != ValidationSeverity.Info)
            .OrderByDescending(x => x.ImportBatch != null ? x.ImportBatch.CreatedAt : x.CreatedAt)
            .Take(100)
            .Select(x => new
            {
                batchNo = x.ImportBatch != null ? x.ImportBatch.BatchNo : string.Empty,
                row = x.RowNumber,
                column = x.ColumnName,
                severity = x.Severity,
                message = x.Message,
                suggestedFix = x.SuggestedFix
            })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("SystemSummary")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> SystemSummary(CancellationToken cancellationToken)
    {
        var result = new
        {
            users = await _db.SystemUsers.CountAsync(cancellationToken),
            roles = await _db.SystemRoles.CountAsync(cancellationToken),
            warehousePermissions = await _db.UserWarehousePermissions.CountAsync(cancellationToken),
            unreadNotifications = await _db.Notifications.CountAsync(x => !x.IsRead, cancellationToken)
        };
        return Json(result);
    }

    [HttpPost("Category")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("CategoryCode", request.CategoryCode), ("Name", request.Name));
        if (validation != null)
        {
            return validation;
        }

        if (await _db.ItemCategories.AnyAsync(x => x.CategoryCode == request.CategoryCode, cancellationToken))
        {
            return Json(new { success = false, message = "Category code already exists." });
        }

        _db.ItemCategories.Add(new ItemCategory { CategoryCode = request.CategoryCode.Trim(), Name = request.Name.Trim(), IsActive = request.IsActive, CreatedBy = _currentUserService.GetCurrentUser().UserName });
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("Category/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Category(int id, CancellationToken cancellationToken)
    {
        var row = await _db.ItemCategories.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.CategoryCode, x.Name, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return row == null ? NotFound() : Json(row);
    }

    [HttpPut("Category/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("CategoryCode", request.CategoryCode), ("Name", request.Name));
        if (validation != null)
        {
            return validation;
        }

        var entity = await _db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        var code = request.CategoryCode.Trim();
        if (await _db.ItemCategories.AnyAsync(x => x.Id != id && x.CategoryCode == code, cancellationToken))
        {
            return Json(new { success = false, message = "Category code already exists." });
        }

        entity.CategoryCode = code;
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        Touch(entity);
        await _db.SaveChangesAsync(cancellationToken);
        AddAudit("Update", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Category/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        Touch(entity);
        AddAudit("SoftDelete", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Category/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = true;
        Touch(entity);
        AddAudit("Restore", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("Category/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _db.ItemCategories.Remove(entity);
        AddAudit("HardDelete", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    [HttpPost("ExternalParty")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExternalParty([FromBody] ExternalPartyRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("PartyCode", request.PartyCode), ("Name", request.Name), ("PartyType", request.PartyType));
        if (validation != null)
        {
            return validation;
        }

        if (!Enum.TryParse<ExternalPartyType>(request.PartyType, true, out var partyType))
        {
            return Json(new { success = false, message = "Party type is invalid." });
        }

        if (await _db.ExternalParties.AnyAsync(x => x.PartyCode == request.PartyCode && x.PartyType == partyType, cancellationToken))
        {
            return Json(new { success = false, message = "Party code already exists." });
        }

        _db.ExternalParties.Add(new ExternalParty
        {
            PartyCode = request.PartyCode.Trim(),
            Name = request.Name.Trim(),
            PartyType = partyType,
            ContactName = request.ContactName,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = request.IsActive,
            CreatedBy = _currentUserService.GetCurrentUser().UserName
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("ExternalParty/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> ExternalParty(int id, CancellationToken cancellationToken)
    {
        var row = await _db.ExternalParties.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.PartyCode, x.Name, PartyType = x.PartyType.ToString(), x.ContactName, x.Phone, x.Email, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        return row == null ? NotFound() : Json(row);
    }

    [HttpPut("ExternalParty/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExternalParty(int id, [FromBody] ExternalPartyRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("PartyCode", request.PartyCode), ("Name", request.Name), ("PartyType", request.PartyType));
        if (validation != null)
        {
            return validation;
        }

        if (!Enum.TryParse<ExternalPartyType>(request.PartyType, true, out var partyType))
        {
            return Json(new { success = false, message = "Party type is invalid." });
        }

        var entity = await _db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        var code = request.PartyCode.Trim();
        if (await _db.ExternalParties.AnyAsync(x => x.Id != id && x.PartyCode == code && x.PartyType == partyType, cancellationToken))
        {
            return Json(new { success = false, message = "Party code already exists." });
        }

        entity.PartyCode = code;
        entity.Name = request.Name.Trim();
        entity.PartyType = partyType;
        entity.ContactName = NullIfWhiteSpace(request.ContactName);
        entity.Phone = NullIfWhiteSpace(request.Phone);
        entity.Email = NullIfWhiteSpace(request.Email);
        entity.IsActive = request.IsActive;
        Touch(entity);
        AddAudit("Update", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("ExternalParty/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        Touch(entity);
        AddAudit("SoftDelete", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("ExternalParty/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = true;
        Touch(entity);
        AddAudit("Restore", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("ExternalParty/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _db.ExternalParties.Remove(entity);
        AddAudit("HardDelete", nameof(ExternalParty), entity.Id, entity.PartyCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    [HttpPost("Item")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem([FromBody] ItemRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("ItemCode", request.ItemCode), ("DefaultName", request.DefaultName), ("UnitCode", request.UnitCode));
        if (validation != null)
        {
            return validation;
        }

        if (await _db.Items.AnyAsync(x => x.ItemCode == request.ItemCode, cancellationToken))
        {
            return Json(new { success = false, message = "Item code already exists." });
        }

        if (!await _db.ItemCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
        {
            return Json(new { success = false, message = "Category is invalid." });
        }

        var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == request.UnitCode, cancellationToken);
        if (unit == null)
        {
            unit = new ItemUnit { UnitCode = request.UnitCode.Trim(), Name = request.UnitName?.Trim() ?? request.UnitCode.Trim(), CreatedBy = _currentUserService.GetCurrentUser().UserName };
            _db.ItemUnits.Add(unit);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var item = new Item
        {
            ItemCode = request.ItemCode.Trim(),
            DefaultName = request.DefaultName.Trim(),
            CategoryId = request.CategoryId,
            UnitId = unit.Id,
            IsSerialManaged = request.IsSerialManaged,
            IsActive = request.IsActive,
            CreatedBy = _currentUserService.GetCurrentUser().UserName
        };
        AddTranslation(item, "vi", request.NameVi);
        AddTranslation(item, "en", request.NameEn);
        AddTranslation(item, "zh", request.NameZh);
        _db.Items.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("Item/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Item(int id, CancellationToken cancellationToken)
    {
        var row = await _db.Items.AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Translations)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ItemCode,
                x.DefaultName,
                x.CategoryId,
                UnitCode = x.Unit != null ? x.Unit.UnitCode : string.Empty,
                UnitName = x.Unit != null ? x.Unit.Name : string.Empty,
                x.IsSerialManaged,
                x.IsActive,
                NameVi = x.Translations.Where(t => t.LanguageCode == "vi" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault(),
                NameEn = x.Translations.Where(t => t.LanguageCode == "en" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault(),
                NameZh = x.Translations.Where(t => t.LanguageCode == "zh" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row == null ? NotFound() : Json(row);
    }

    [HttpPut("Item/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("ItemCode", request.ItemCode), ("DefaultName", request.DefaultName), ("UnitCode", request.UnitCode));
        if (validation != null)
        {
            return validation;
        }

        var item = await _db.Items.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }

        var code = request.ItemCode.Trim();
        if (await _db.Items.AnyAsync(x => x.Id != id && x.ItemCode == code, cancellationToken))
        {
            return Json(new { success = false, message = "Item code already exists." });
        }

        if (!await _db.ItemCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
        {
            return Json(new { success = false, message = "Category is invalid." });
        }

        var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == request.UnitCode, cancellationToken);
        if (unit == null)
        {
            unit = new ItemUnit { UnitCode = request.UnitCode.Trim(), Name = request.UnitName?.Trim() ?? request.UnitCode.Trim(), CreatedBy = _currentUserService.GetCurrentUser().UserName };
            _db.ItemUnits.Add(unit);
            await _db.SaveChangesAsync(cancellationToken);
        }

        item.ItemCode = code;
        item.DefaultName = request.DefaultName.Trim();
        item.CategoryId = request.CategoryId;
        item.UnitId = unit.Id;
        item.IsSerialManaged = request.IsSerialManaged;
        item.IsActive = request.IsActive;
        Touch(item);
        UpsertTranslation(item, "vi", request.NameVi);
        UpsertTranslation(item, "en", request.NameEn);
        UpsertTranslation(item, "zh", request.NameZh);
        AddAudit("Update", nameof(Item), item.Id, item.ItemCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Item/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateItem(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Items.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        Touch(entity);
        AddAudit("SoftDelete", nameof(Item), entity.Id, entity.ItemCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Item/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreItem(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Items.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.IsActive = true;
        Touch(entity);
        AddAudit("Restore", nameof(Item), entity.Id, entity.ItemCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("Item/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Items.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _db.ItemTranslations.RemoveRange(entity.Translations);
        _db.Items.Remove(entity);
        AddAudit("HardDelete", nameof(Item), entity.Id, entity.ItemCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    [HttpPost("WarehouseStructure")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWarehouseStructure([FromBody] WarehouseStructureRequest request, CancellationToken cancellationToken)
    {
        var validation = request.WarehouseId.HasValue
            ? ValidateRequired(
            ("ZoneCode", request.ZoneCode),
            ("ZoneName", request.ZoneName),
            ("RackCode", request.RackCode),
            ("RackName", request.RackName),
            ("ShelfCode", request.ShelfCode),
            ("ShelfName", request.ShelfName))
            : ValidateRequired(
            ("CompanyCode", request.CompanyCode),
            ("CompanyName", request.CompanyName),
            ("BranchCode", request.BranchCode),
            ("BranchName", request.BranchName),
            ("WarehouseCode", request.WarehouseCode),
            ("WarehouseName", request.WarehouseName),
            ("ZoneCode", request.ZoneCode),
            ("ZoneName", request.ZoneName),
            ("RackCode", request.RackCode),
            ("RackName", request.RackName),
            ("ShelfCode", request.ShelfCode),
            ("ShelfName", request.ShelfName));
        if (validation != null)
        {
            return validation;
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var existingWarehouse = request.WarehouseId.HasValue
            ? await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken)
            : await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken);
        if (request.WarehouseId.HasValue && existingWarehouse == null)
        {
            return Json(new { success = false, message = "Warehouse is invalid." });
        }
        if (!currentUser.IsAdmin && (existingWarehouse == null || !currentUser.CanAccessWarehouse(existingWarehouse.Id)))
        {
            return Json(new { success = false, message = "Current role cannot manage this warehouse." });
        }

        var user = _currentUserService.GetCurrentUser().UserName;
        Warehouse warehouse;
        if (request.WarehouseId.HasValue)
        {
            warehouse = await _db.Warehouses.FirstAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken);
        }
        else
        {
            var company = await _db.Companies.FirstOrDefaultAsync(x => x.Code == request.CompanyCode, cancellationToken)
            ?? _db.Companies.Add(new Company { Code = request.CompanyCode, Name = request.CompanyName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);

            var branch = await _db.Branches.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == request.BranchCode, cancellationToken)
            ?? _db.Branches.Add(new Branch { CompanyId = company.Id, Code = request.BranchCode, Name = request.BranchName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);

            warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken)
            ?? _db.Warehouses.Add(new Warehouse { BranchId = branch.Id, WarehouseCode = request.WarehouseCode, Name = request.WarehouseName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var zone = await _db.WarehouseZones.FirstOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.ZoneCode == request.ZoneCode, cancellationToken)
            ?? _db.WarehouseZones.Add(new WarehouseZone { WarehouseId = warehouse.Id, ZoneCode = request.ZoneCode, Name = request.ZoneName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        var rack = await _db.Racks.FirstOrDefaultAsync(x => x.WarehouseZoneId == zone.Id && x.RackCode == request.RackCode, cancellationToken)
            ?? _db.Racks.Add(new Rack { WarehouseZoneId = zone.Id, RackCode = request.RackCode, Name = request.RackName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        var shelf = await _db.Shelves.FirstOrDefaultAsync(x => x.RackId == rack.Id && x.ShelfCode == request.ShelfCode, cancellationToken)
            ?? _db.Shelves.Add(new Shelf { RackId = rack.Id, ShelfCode = request.ShelfCode, Name = request.ShelfName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        var binCode = BuildBinCode(warehouse.WarehouseCode, request.RackCode, request.ShelfCode, request.BinCode);
        if (await _db.BinLocations.AnyAsync(x => x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
        {
            return Json(new { success = false, message = "Bin code already exists in warehouse." });
        }

        var bin = _db.BinLocations.Add(new BinLocation
        {
            WarehouseId = warehouse.Id,
            ShelfId = shelf.Id,
            BinCode = binCode,
            FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}",
            IsActive = request.IsActive,
            CreatedBy = user
        }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        AddAudit("Create", nameof(BinLocation), bin.Id, bin.BinCode);
        await _db.SaveChangesAsync(cancellationToken);

        return Json(new { success = true, data = new { warehouseId = warehouse.Id, binLocationId = bin.Id, binCode = bin.BinCode } });
    }

    [HttpGet("WarehouseStructure/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> WarehouseStructureDetail(int id, CancellationToken cancellationToken)
    {
        var row = await _db.BinLocations.AsNoTracking()
            .Include(x => x.Warehouse)!.ThenInclude(x => x!.Branch)!.ThenInclude(x => x!.Company)
            .Include(x => x.Shelf)!.ThenInclude(x => x!.Rack)!.ThenInclude(x => x!.WarehouseZone)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                WarehouseId = x.WarehouseId,
                CompanyCode = x.Warehouse != null && x.Warehouse.Branch != null && x.Warehouse.Branch.Company != null ? x.Warehouse.Branch.Company.Code : string.Empty,
                CompanyName = x.Warehouse != null && x.Warehouse.Branch != null && x.Warehouse.Branch.Company != null ? x.Warehouse.Branch.Company.Name : string.Empty,
                BranchCode = x.Warehouse != null && x.Warehouse.Branch != null ? x.Warehouse.Branch.Code : string.Empty,
                BranchName = x.Warehouse != null && x.Warehouse.Branch != null ? x.Warehouse.Branch.Name : string.Empty,
                WarehouseCode = x.Warehouse != null ? x.Warehouse.WarehouseCode : string.Empty,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : string.Empty,
                ZoneCode = x.Shelf != null && x.Shelf.Rack != null && x.Shelf.Rack.WarehouseZone != null ? x.Shelf.Rack.WarehouseZone.ZoneCode : string.Empty,
                ZoneName = x.Shelf != null && x.Shelf.Rack != null && x.Shelf.Rack.WarehouseZone != null ? x.Shelf.Rack.WarehouseZone.Name : string.Empty,
                RackCode = x.Shelf != null && x.Shelf.Rack != null ? x.Shelf.Rack.RackCode : string.Empty,
                RackName = x.Shelf != null && x.Shelf.Rack != null ? x.Shelf.Rack.Name : string.Empty,
                ShelfCode = x.Shelf != null ? x.Shelf.ShelfCode : string.Empty,
                ShelfName = x.Shelf != null ? x.Shelf.Name : string.Empty,
                x.BinCode,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            return NotFound();
        }

        var user = _currentUserService.GetCurrentUser();
        var warehouse = await _db.BinLocations.AsNoTracking().Where(x => x.Id == id).Select(x => x.WarehouseId).FirstAsync(cancellationToken);
        if (!user.CanAccessWarehouse(warehouse))
        {
            return Forbid();
        }

        return Json(row);
    }

    [HttpPut("WarehouseStructure/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWarehouseStructure(int id, [FromBody] WarehouseStructureRequest request, CancellationToken cancellationToken)
    {
        var bin = await _db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (bin == null)
        {
            return NotFound();
        }

        var userContext = _currentUserService.GetCurrentUser();
        if (!userContext.CanAccessWarehouse(bin.WarehouseId))
        {
            return Forbid();
        }

        var validation = request.WarehouseId.HasValue
            ? ValidateRequired(
            ("ZoneCode", request.ZoneCode),
            ("ZoneName", request.ZoneName),
            ("RackCode", request.RackCode),
            ("RackName", request.RackName),
            ("ShelfCode", request.ShelfCode),
            ("ShelfName", request.ShelfName))
            : ValidateRequired(
            ("CompanyCode", request.CompanyCode),
            ("CompanyName", request.CompanyName),
            ("BranchCode", request.BranchCode),
            ("BranchName", request.BranchName),
            ("WarehouseCode", request.WarehouseCode),
            ("WarehouseName", request.WarehouseName),
            ("ZoneCode", request.ZoneCode),
            ("ZoneName", request.ZoneName),
            ("RackCode", request.RackCode),
            ("RackName", request.RackName),
            ("ShelfCode", request.ShelfCode),
            ("ShelfName", request.ShelfName));
        if (validation != null)
        {
            return validation;
        }

        var targetWarehouse = request.WarehouseId.HasValue
            ? await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken)
            : await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken);
        if (request.WarehouseId.HasValue && targetWarehouse == null)
        {
            return Json(new { success = false, message = "Warehouse is invalid." });
        }
        if (!userContext.IsAdmin && (targetWarehouse == null || !userContext.CanAccessWarehouse(targetWarehouse.Id)))
        {
            return Json(new { success = false, message = "Current role cannot manage this warehouse." });
        }

        var user = userContext.UserName;
        Warehouse warehouse;
        if (request.WarehouseId.HasValue)
        {
            warehouse = await _db.Warehouses.FirstAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken);
        }
        else
        {
            var company = await _db.Companies.FirstOrDefaultAsync(x => x.Code == request.CompanyCode, cancellationToken)
            ?? _db.Companies.Add(new Company { Code = request.CompanyCode, Name = request.CompanyName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
            var branch = await _db.Branches.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == request.BranchCode, cancellationToken)
            ?? _db.Branches.Add(new Branch { CompanyId = company.Id, Code = request.BranchCode, Name = request.BranchName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
            warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken)
            ?? _db.Warehouses.Add(new Warehouse { BranchId = branch.Id, WarehouseCode = request.WarehouseCode, Name = request.WarehouseName, CreatedBy = user }).Entity;
            await _db.SaveChangesAsync(cancellationToken);
        }
        var zone = await _db.WarehouseZones.FirstOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.ZoneCode == request.ZoneCode, cancellationToken)
            ?? _db.WarehouseZones.Add(new WarehouseZone { WarehouseId = warehouse.Id, ZoneCode = request.ZoneCode, Name = request.ZoneName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);
        var rack = await _db.Racks.FirstOrDefaultAsync(x => x.WarehouseZoneId == zone.Id && x.RackCode == request.RackCode, cancellationToken)
            ?? _db.Racks.Add(new Rack { WarehouseZoneId = zone.Id, RackCode = request.RackCode, Name = request.RackName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);
        var shelf = await _db.Shelves.FirstOrDefaultAsync(x => x.RackId == rack.Id && x.ShelfCode == request.ShelfCode, cancellationToken)
            ?? _db.Shelves.Add(new Shelf { RackId = rack.Id, ShelfCode = request.ShelfCode, Name = request.ShelfName, CreatedBy = user }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        var binCode = BuildBinCode(warehouse.WarehouseCode, request.RackCode, request.ShelfCode, request.BinCode);
        if (await _db.BinLocations.AnyAsync(x => x.Id != id && x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
        {
            return Json(new { success = false, message = "Bin code already exists in warehouse." });
        }

        bin.WarehouseId = warehouse.Id;
        bin.ShelfId = shelf.Id;
        bin.BinCode = binCode;
        bin.FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}";
        bin.IsActive = request.IsActive;
        Touch(bin);
        AddAudit("Update", nameof(BinLocation), bin.Id, bin.BinCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, data = new { warehouseId = warehouse.Id, binLocationId = bin.Id, binCode = bin.BinCode } });
    }

    [HttpPost("WarehouseStructure/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateWarehouseStructure(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        if (!_currentUserService.GetCurrentUser().CanAccessWarehouse(entity.WarehouseId))
        {
            return Forbid();
        }

        entity.IsActive = false;
        Touch(entity);
        AddAudit("SoftDelete", nameof(BinLocation), entity.Id, entity.BinCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("WarehouseStructure/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreWarehouseStructure(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        if (!_currentUserService.GetCurrentUser().CanAccessWarehouse(entity.WarehouseId))
        {
            return Forbid();
        }

        entity.IsActive = true;
        Touch(entity);
        AddAudit("Restore", nameof(BinLocation), entity.Id, entity.BinCode);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("WarehouseStructure/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWarehouseStructure(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _db.BinLocations.Remove(entity);
        AddAudit("HardDelete", nameof(BinLocation), entity.Id, entity.BinCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    [HttpGet("Roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var rows = await _db.SystemRoles.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Description })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    [HttpGet("Users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users([FromQuery] string? keyword = null, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var key = keyword?.Trim();
        var query = _db.SystemUsers.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            query = query.Where(x => x.UserName.Contains(key) || x.DisplayName.Contains(key) || x.Email.Contains(key));
        }

        var users = await query.OrderBy(x => x.UserName).Take(100).ToArrayAsync(cancellationToken);
        var userIds = users.Select(x => x.Id).ToArray();
        var permissions = await _db.UserWarehousePermissions.AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => userIds.Contains(x.UserId))
            .ToArrayAsync(cancellationToken);

        var rows = users.Select(x => new
        {
            x.Id,
            x.UserName,
            x.DisplayName,
            x.Email,
            x.PreferredLanguage,
            x.IsActive,
            Roles = x.UserRoles.Select(r => r.Role != null ? r.Role.Name : string.Empty).Where(r => r.Length > 0).OrderBy(r => r).ToArray(),
            Warehouses = permissions.Where(p => p.UserId == x.Id).Select(p => p.Warehouse != null ? p.Warehouse.WarehouseCode : p.WarehouseId.ToString()).OrderBy(p => p).ToArray()
        });
        return Json(rows);
    }

    [HttpGet("User/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UserDetail(string id, CancellationToken cancellationToken)
    {
        var user = await _db.SystemUsers.AsNoTracking()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var warehouseIds = await _db.UserWarehousePermissions.AsNoTracking()
            .Where(x => x.UserId == id && x.CanView)
            .Select(x => x.WarehouseId)
            .ToArrayAsync(cancellationToken);
        return Json(new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            user.PreferredLanguage,
            user.IsActive,
            RoleIds = user.UserRoles.Select(x => x.RoleId).ToArray(),
            WarehouseIds = warehouseIds
        });
    }

    [HttpPost("User")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser([FromBody] UserRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("UserName", request.UserName), ("DisplayName", request.DisplayName), ("Password", request.Password));
        if (validation != null)
        {
            return validation;
        }

        var normalized = request.UserName.Trim().ToUpperInvariant();
        if (await _db.SystemUsers.AnyAsync(x => x.NormalizedUserName == normalized, cancellationToken))
        {
            return Json(new { success = false, message = "User name already exists." });
        }

        var user = new SystemUser
        {
            UserName = request.UserName.Trim(),
            NormalizedUserName = normalized,
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            PreferredLanguage = NormalizeLanguage(request.PreferredLanguage),
            PasswordHash = PasswordHashService.Hash(request.Password ?? string.Empty),
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        _db.SystemUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var accessError = await ReplaceUserAccessAsync(user.Id, request.RoleIds, request.WarehouseIds, cancellationToken);
        if (accessError != null)
        {
            _db.SystemUsers.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);
            return Json(new { success = false, message = accessError });
        }

        AddAudit("Create", nameof(SystemUser), null, user.UserName);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, id = user.Id });
    }

    [HttpPut("User/{id}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UserRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("UserName", request.UserName), ("DisplayName", request.DisplayName));
        if (validation != null)
        {
            return validation;
        }

        var user = await _db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var normalized = request.UserName.Trim().ToUpperInvariant();
        if (await _db.SystemUsers.AnyAsync(x => x.Id != id && x.NormalizedUserName == normalized, cancellationToken))
        {
            return Json(new { success = false, message = "User name already exists." });
        }

        if (id == _currentUserService.GetCurrentUser().UserId)
        {
            var selectedRoleNames = await _db.SystemRoles.AsNoTracking().Where(x => request.RoleIds.Contains(x.Id)).Select(x => x.Name).ToArrayAsync(cancellationToken);
            if (!selectedRoleNames.Contains("Admin", StringComparer.OrdinalIgnoreCase) || !request.IsActive)
            {
                return Json(new { success = false, message = "You cannot remove your own admin access." });
            }
        }

        user.UserName = request.UserName.Trim();
        user.NormalizedUserName = normalized;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email?.Trim() ?? string.Empty;
        user.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage);
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHashService.Hash(request.Password);
        }

        var accessError = await ReplaceUserAccessAsync(user.Id, request.RoleIds, request.WarehouseIds, cancellationToken);
        if (accessError != null)
        {
            return Json(new { success = false, message = accessError });
        }

        AddAudit("Update", nameof(SystemUser), null, user.UserName);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("User/{id}/Deactivate")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(string id, CancellationToken cancellationToken)
    {
        if (id == _currentUserService.GetCurrentUser().UserId)
        {
            return Json(new { success = false, message = "You cannot deactivate your own account." });
        }

        var user = await _db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = false;
        AddAudit("SoftDelete", nameof(SystemUser), null, user.UserName);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("User/{id}/Restore")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreUser(string id, CancellationToken cancellationToken)
    {
        var user = await _db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = true;
        AddAudit("Restore", nameof(SystemUser), null, user.UserName);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("User/{id}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        if (id == _currentUserService.GetCurrentUser().UserId)
        {
            return Json(new { success = false, message = "You cannot hard delete your own account." });
        }

        var user = await _db.SystemUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        _db.SystemUserRoles.RemoveRange(_db.SystemUserRoles.Where(x => x.UserId == id));
        _db.UserWarehousePermissions.RemoveRange(_db.UserWarehousePermissions.Where(x => x.UserId == id));
        _db.Notifications.RemoveRange(_db.Notifications.Where(x => x.UserId == id));
        _db.SystemUsers.Remove(user);
        AddAudit("HardDelete", nameof(SystemUser), null, user.UserName);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    private static void AddTranslation(Item item, string languageCode, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            item.Translations.Add(new ItemTranslation { LanguageCode = languageCode, FieldName = "DefaultName", Value = value.Trim(), CreatedBy = item.CreatedBy });
        }
    }

    private void UpsertTranslation(Item item, string languageCode, string? value)
    {
        var existing = item.Translations.FirstOrDefault(x => x.LanguageCode == languageCode && x.FieldName == "DefaultName");
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing != null)
            {
                _db.ItemTranslations.Remove(existing);
            }

            return;
        }

        if (existing == null)
        {
            item.Translations.Add(new ItemTranslation { LanguageCode = languageCode, FieldName = "DefaultName", Value = value.Trim(), CreatedBy = _currentUserService.GetCurrentUser().UserName });
            return;
        }

        existing.Value = value.Trim();
        Touch(existing);
    }

    private async Task<string?> ReplaceUserAccessAsync(string userId, IReadOnlyCollection<int> roleIds, IReadOnlyCollection<int> warehouseIds, CancellationToken cancellationToken)
    {
        var distinctRoleIds = roleIds.Distinct().ToArray();
        if (distinctRoleIds.Length == 0)
        {
            return "At least one role is required.";
        }

        var roles = await _db.SystemRoles.Where(x => distinctRoleIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
        if (roles.Length != distinctRoleIds.Length)
        {
            return "One or more roles are invalid.";
        }

        var isAdminRole = roles.Any(x => x.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        var canManage = isAdminRole || roles.Any(x => x.Name.Equals("Warehouse Manager", StringComparison.OrdinalIgnoreCase));
        var canOperate = canManage || roles.Any(x => x.Name.Equals("Warehouse Staff", StringComparison.OrdinalIgnoreCase));
        var distinctWarehouseIds = warehouseIds.Distinct().Where(x => x > 0).ToArray();
        if (!isAdminRole && distinctWarehouseIds.Length == 0)
        {
            return "Non-admin users must be assigned to at least one warehouse.";
        }

        var validWarehouseIds = await _db.Warehouses.Where(x => distinctWarehouseIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToArrayAsync(cancellationToken);
        if (validWarehouseIds.Length != distinctWarehouseIds.Length)
        {
            return "One or more warehouses are invalid.";
        }

        _db.SystemUserRoles.RemoveRange(_db.SystemUserRoles.Where(x => x.UserId == userId));
        _db.UserWarehousePermissions.RemoveRange(_db.UserWarehousePermissions.Where(x => x.UserId == userId));
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var role in roles)
        {
            _db.SystemUserRoles.Add(new SystemUserRole { UserId = userId, RoleId = role.Id });
        }

        foreach (var warehouseId in validWarehouseIds)
        {
            _db.UserWarehousePermissions.Add(new UserWarehousePermission
            {
                UserId = userId,
                WarehouseId = warehouseId,
                CanView = true,
                CanOperate = canOperate,
                CanManage = canManage,
                CreatedBy = _currentUserService.GetCurrentUser().UserName
            });
        }

        return null;
    }

    private IActionResult? ValidateRequired(params (string Field, string? Value)[] fields)
    {
        var missing = fields.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Field} is required.").ToArray();
        return missing.Length == 0 ? null : Json(new { success = false, message = string.Join(" ", missing), errors = missing });
    }

    private void Touch(AuditableEntity entity)
    {
        entity.UpdatedAt = DateTime.Now;
        entity.UpdatedBy = _currentUserService.GetCurrentUser().UserName;
    }

    private void AddAudit(string action, string entityName, int? entityId, string? referenceNo)
    {
        var user = _currentUserService.GetCurrentUser();
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNo = referenceNo,
            Result = "Success",
            CreatedAt = DateTime.Now
        });
    }

    private async Task<IActionResult> TrySaveDeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return Json(new { success = true });
        }
        catch (DbUpdateException)
        {
            return Json(new { success = false, message = "Cannot hard delete this record because it is referenced by operational data. Use soft delete instead." });
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildBinCode(string warehouseCode, string rackCode, string shelfCode, string? requestedBinCode)
    {
        if (!string.IsNullOrWhiteSpace(requestedBinCode))
        {
            return requestedBinCode.Trim();
        }

        static string Normalize(string value) => value.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        return string.Join("_", new[] { Normalize(warehouseCode), Normalize(rackCode), Normalize(shelfCode) }.Where(x => x.Length > 0));
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }

    public sealed class CategoryRequest
    {
        public string CategoryCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; } = true;
    }

    public sealed class ExternalPartyRequest
    {
        public string PartyCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string PartyType { get; init; } = string.Empty;
        public string? ContactName { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public sealed class ItemRequest
    {
        public string ItemCode { get; init; } = string.Empty;
        public string DefaultName { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public string UnitCode { get; init; } = "PCS";
        public string? UnitName { get; init; }
        public bool IsSerialManaged { get; init; }
        public bool IsActive { get; init; } = true;
        public string? NameVi { get; init; }
        public string? NameEn { get; init; }
        public string? NameZh { get; init; }
    }

    public sealed class WarehouseStructureRequest
    {
        public int? WarehouseId { get; init; }
        public string CompanyCode { get; init; } = string.Empty;
        public string CompanyName { get; init; } = string.Empty;
        public string BranchCode { get; init; } = string.Empty;
        public string BranchName { get; init; } = string.Empty;
        public string WarehouseCode { get; init; } = string.Empty;
        public string WarehouseName { get; init; } = string.Empty;
        public string ZoneCode { get; init; } = string.Empty;
        public string ZoneName { get; init; } = string.Empty;
        public string RackCode { get; init; } = string.Empty;
        public string RackName { get; init; } = string.Empty;
        public string ShelfCode { get; init; } = string.Empty;
        public string ShelfName { get; init; } = string.Empty;
        public string BinCode { get; init; } = string.Empty;
        public bool IsActive { get; init; } = true;
    }

    public sealed class UserRequest
    {
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? Password { get; init; }
        public string? PreferredLanguage { get; init; } = "vi";
        public bool IsActive { get; init; } = true;
        public IReadOnlyCollection<int> RoleIds { get; init; } = Array.Empty<int>();
        public IReadOnlyCollection<int> WarehouseIds { get; init; } = Array.Empty<int>();
    }
}
