using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

/// <summary>Categories, ExternalParties, Items, MasterDataList, MasterDataSummary</summary>
[Route("[controller]")]
public sealed class MasterDataController : ManagementBaseController
{
    public MasterDataController(InventoryDbContext db, ICurrentUserService currentUserService)
        : base(db, currentUserService) { }

    // ─── Master Data Summary & List ────────────────────────────
    [HttpGet("Summary")]
    public async Task<IActionResult> MasterDataSummary(CancellationToken cancellationToken)
    {
        var result = new
        {
            items = await Db.Items.CountAsync(cancellationToken),
            serialManaged = await Db.Items.CountAsync(x => x.IsSerialManaged, cancellationToken),
            categories = await Db.ItemCategories.CountAsync(cancellationToken),
            externalParties = await Db.ExternalParties.CountAsync(cancellationToken),
            translations = await Db.ItemTranslations.CountAsync(cancellationToken) + await Db.Translations.CountAsync(cancellationToken)
        };
        return Json(result);
    }

    [HttpGet("List")]
    public async Task<IActionResult> MasterDataList([FromQuery] string entity = "items", [FromQuery] string? keyword = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var skip = pageSize == 0 ? 0 : (page - 1) * pageSize;
        var key = string.IsNullOrWhiteSpace(keyword)? null: keyword.Trim();
        var normalizedEntity = string.IsNullOrWhiteSpace(entity)? "items": entity.Trim().ToLowerInvariant();

        switch (normalizedEntity)
        {
            case "categories":
                {
                    var query = Db.ItemCategories.AsNoTracking().Where(x =>(!isActive.HasValue || x.IsActive == isActive.Value) &&
                            (key == null ||x.CategoryCode.Contains(key) ||x.Name.Contains(key)));
                    var total = await query.CountAsync(cancellationToken);
                    var rows = await query.OrderBy(x => x.CategoryCode).Skip(skip).Take(pageSize == 0 ? total : pageSize)
                        .Select(x => new
                        {
                            id = x.Id,
                            entity = "categories",
                            code = x.CategoryCode,
                            name = x.Name,
                            type = "Category",
                            serialTracking = "-",
                            languageCoverage = "-",
                            status = x.IsActive ? "Active" : "Inactive",
                            isActive = x.IsActive
                        }).ToListAsync(cancellationToken);
                    return Json(new
                    {
                        page,
                        pageSize,
                        total,
                        data = rows
                    });
                }
            case "parties":
                {
                    var query = Db.ExternalParties.AsNoTracking().Where(x =>(!isActive.HasValue || x.IsActive == isActive.Value) &&(key == null ||
                             x.PartyCode.Contains(key) || x.Name.Contains(key)));
                    var total = await query.CountAsync(cancellationToken);
                    var rows = await query.OrderBy(x => x.PartyCode).Skip(skip).Take(pageSize == 0 ? total : pageSize)
                        .Select(x => new
                        {
                            id = x.Id,
                            entity = "parties",
                            code = x.PartyCode,
                            name = x.Name,
                            type = x.PartyType.ToString(),
                            serialTracking = "-",
                            languageCoverage = "-",
                            status = x.IsActive ? "Active" : "Inactive",
                            isActive = x.IsActive
                        }).ToListAsync(cancellationToken);
                    return Json(new
                    {
                        page,
                        pageSize,
                        total,
                        data = rows
                    });
                }

            default:
                {
                    var query = Db.Items.AsNoTracking().Where(x =>(!isActive.HasValue || x.IsActive == isActive.Value) && (key == null ||x.ItemCode.Contains(key) ||x.DefaultName.Contains(key)));
                    var total = await query.CountAsync(cancellationToken);
                    var rows = await query.OrderBy(x => x.ItemCode).Skip(skip).Take(pageSize == 0 ? total : pageSize)
                        .Select(x => new
                        {
                            id = x.Id,
                            entity = "items",
                            code = x.ItemCode,
                            name = x.DefaultName,
                            type = x.Category != null? "Item / " + x.Category.CategoryCode: "Item",
                            serialTracking = x.IsSerialManaged? "Yes": "No",
                            languageCoverage = "-",//string.Join(" / ",x.Translations.Select(t => t.LanguageCode).Distinct().OrderBy(t => t)),
                            status = x.IsActive? "Active": "Inactive",
                            isActive = x.IsActive
                        }).ToListAsync(cancellationToken);
                    return Json(new
                    {
                        page,
                        pageSize,
                        total,
                        data = rows
                    });
                }
        }
    }

    // ─── Category CRUD ─────────────────────────────────────────
    [HttpPost("Category")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("CategoryCode", request.CategoryCode), ("Name", request.Name));
        if (validation != null) return validation;

        if (await Db.ItemCategories.AnyAsync(x => x.CategoryCode == request.CategoryCode, cancellationToken))
            return Json(new { success = false, message = "Category code already exists." });

        Db.ItemCategories.Add(new ItemCategory { CategoryCode = request.CategoryCode.Trim(), Name = request.Name.Trim(), IsActive = request.IsActive, CreatedBy = CurrentUserService.GetCurrentUser().UserName });
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("Category/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Category(int id, CancellationToken cancellationToken)
    {
        var row = await Db.ItemCategories.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.CategoryCode, x.Name, x.IsActive }).FirstOrDefaultAsync(cancellationToken);
        return row == null ? NotFound() : Json(row);
    }

    [HttpPut("Category/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("CategoryCode", request.CategoryCode), ("Name", request.Name));
        if (validation != null) return validation;

        var entity = await Db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();

        var code = request.CategoryCode.Trim();
        if (await Db.ItemCategories.AnyAsync(x => x.Id != id && x.CategoryCode == code, cancellationToken))
            return Json(new { success = false, message = "Category code already exists." });

        entity.CategoryCode = code; entity.Name = request.Name.Trim(); entity.IsActive = request.IsActive;
        Touch(entity); AddAudit("Update", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Category/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = false; Touch(entity); AddAudit("SoftDelete", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Category/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = true; Touch(entity); AddAudit("Restore", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("Category/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        Db.ItemCategories.Remove(entity); AddAudit("HardDelete", nameof(ItemCategory), entity.Id, entity.CategoryCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    // ─── ExternalParty CRUD ────────────────────────────────────
    [HttpPost("ExternalParty")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExternalParty([FromBody] ExternalPartyRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("PartyCode", request.PartyCode), ("Name", request.Name), ("PartyType", request.PartyType));
        if (validation != null) return validation;
        if (!Enum.TryParse<ExternalPartyType>(request.PartyType, true, out var partyType))
            return Json(new { success = false, message = "Party type is invalid." });
        if (await Db.ExternalParties.AnyAsync(x => x.PartyCode == request.PartyCode && x.PartyType == partyType, cancellationToken))
            return Json(new { success = false, message = "Party code already exists." });

        Db.ExternalParties.Add(new ExternalParty
        {
            PartyCode = request.PartyCode.Trim(), Name = request.Name.Trim(), PartyType = partyType,
            ContactName = request.ContactName, Phone = request.Phone, Email = request.Email,
            IsActive = request.IsActive, CreatedBy = CurrentUserService.GetCurrentUser().UserName
        });
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("ExternalParty/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> ExternalParty(int id, CancellationToken cancellationToken)
    {
        var row = await Db.ExternalParties.AsNoTracking().Where(x => x.Id == id)
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
        if (validation != null) return validation;
        if (!Enum.TryParse<ExternalPartyType>(request.PartyType, true, out var partyType))
            return Json(new { success = false, message = "Party type is invalid." });

        var entity = await Db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();

        var code = request.PartyCode.Trim();
        if (await Db.ExternalParties.AnyAsync(x => x.Id != id && x.PartyCode == code && x.PartyType == partyType, cancellationToken))
            return Json(new { success = false, message = "Party code already exists." });

        entity.PartyCode = code; entity.Name = request.Name.Trim(); entity.PartyType = partyType;
        entity.ContactName = NullIfWhiteSpace(request.ContactName); entity.Phone = NullIfWhiteSpace(request.Phone); entity.Email = NullIfWhiteSpace(request.Email);
        entity.IsActive = request.IsActive;
        Touch(entity); AddAudit("Update", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("ExternalParty/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = false; Touch(entity); AddAudit("SoftDelete", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("ExternalParty/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = true; Touch(entity); AddAudit("Restore", nameof(ExternalParty), entity.Id, entity.PartyCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("ExternalParty/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExternalParty(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.ExternalParties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        Db.ExternalParties.Remove(entity); AddAudit("HardDelete", nameof(ExternalParty), entity.Id, entity.PartyCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    // ─── Item CRUD ─────────────────────────────────────────────
    [HttpPost("Item")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem([FromBody] ItemRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("ItemCode", request.ItemCode), ("DefaultName", request.DefaultName), ("UnitCode", request.UnitCode));
        if (validation != null) return validation;

        if (await Db.Items.AnyAsync(x => x.ItemCode == request.ItemCode, cancellationToken))
            return Json(new { success = false, message = "Item code already exists." });
        if (!await Db.ItemCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
            return Json(new { success = false, message = "Category is invalid." });

        var unit = await Db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == request.UnitCode, cancellationToken);
        if (unit == null)
        {
            unit = new ItemUnit { UnitCode = request.UnitCode.Trim(), Name = request.UnitName?.Trim() ?? request.UnitCode.Trim(), CreatedBy = CurrentUserService.GetCurrentUser().UserName };
            Db.ItemUnits.Add(unit);
            await Db.SaveChangesAsync(cancellationToken);
        }

        var item = new Item
        {
            ItemCode = request.ItemCode.Trim(), DefaultName = request.DefaultName.Trim(),
            CategoryId = request.CategoryId, UnitId = unit.Id,
            IsSerialManaged = request.IsSerialManaged, IsActive = request.IsActive,
            CreatedBy = CurrentUserService.GetCurrentUser().UserName
        };
        AddTranslation(item, "vi", request.NameVi); AddTranslation(item, "en", request.NameEn); AddTranslation(item, "zh", request.NameZh);
        Db.Items.Add(item);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpGet("Item/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Item(int id, CancellationToken cancellationToken)
    {
        var row = await Db.Items.AsNoTracking().Include(x => x.Unit).Include(x => x.Translations).Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.ItemCode, x.DefaultName, x.CategoryId,
                UnitCode = x.Unit != null ? x.Unit.UnitCode : string.Empty, UnitName = x.Unit != null ? x.Unit.Name : string.Empty,
                x.IsSerialManaged, x.IsActive,
                NameVi = x.Translations.Where(t => t.LanguageCode == "vi" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault(),
                NameEn = x.Translations.Where(t => t.LanguageCode == "en" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault(),
                NameZh = x.Translations.Where(t => t.LanguageCode == "zh" && t.FieldName == "DefaultName").Select(t => t.Value).FirstOrDefault()
            }).FirstOrDefaultAsync(cancellationToken);
        return row == null ? NotFound() : Json(row);
    }

    [HttpPut("Item/{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateRequired(("ItemCode", request.ItemCode), ("DefaultName", request.DefaultName), ("UnitCode", request.UnitCode));
        if (validation != null) return validation;

        var item = await Db.Items.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item == null) return NotFound();

        var code = request.ItemCode.Trim();
        if (await Db.Items.AnyAsync(x => x.Id != id && x.ItemCode == code, cancellationToken))
            return Json(new { success = false, message = "Item code already exists." });
        if (!await Db.ItemCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
            return Json(new { success = false, message = "Category is invalid." });

        var unit = await Db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == request.UnitCode, cancellationToken);
        if (unit == null)
        {
            unit = new ItemUnit { UnitCode = request.UnitCode.Trim(), Name = request.UnitName?.Trim() ?? request.UnitCode.Trim(), CreatedBy = CurrentUserService.GetCurrentUser().UserName };
            Db.ItemUnits.Add(unit); await Db.SaveChangesAsync(cancellationToken);
        }

        item.ItemCode = code; item.DefaultName = request.DefaultName.Trim(); item.CategoryId = request.CategoryId;
        item.UnitId = unit.Id; item.IsSerialManaged = request.IsSerialManaged; item.IsActive = request.IsActive;
        Touch(item);
        UpsertTranslation(item, "vi", request.NameVi); UpsertTranslation(item, "en", request.NameEn); UpsertTranslation(item, "zh", request.NameZh);
        AddAudit("Update", nameof(Item), item.Id, item.ItemCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Item/{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateItem(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.Items.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = false; Touch(entity); AddAudit("SoftDelete", nameof(Item), entity.Id, entity.ItemCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("Item/{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreItem(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.Items.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        entity.IsActive = true; Touch(entity); AddAudit("Restore", nameof(Item), entity.Id, entity.ItemCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("Item/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.Items.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        Db.ItemTranslations.RemoveRange(entity.Translations); Db.Items.Remove(entity);
        AddAudit("HardDelete", nameof(Item), entity.Id, entity.ItemCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    // ─── Translation helpers ───────────────────────────────────
    private static void AddTranslation(Item item, string languageCode, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            item.Translations.Add(new ItemTranslation { LanguageCode = languageCode, FieldName = "DefaultName", Value = value.Trim(), CreatedBy = item.CreatedBy });
    }

    private void UpsertTranslation(Item item, string languageCode, string? value)
    {
        var existing = item.Translations.FirstOrDefault(x => x.LanguageCode == languageCode && x.FieldName == "DefaultName");
        if (string.IsNullOrWhiteSpace(value)) { if (existing != null) Db.ItemTranslations.Remove(existing); return; }
        if (existing == null) { item.Translations.Add(new ItemTranslation { LanguageCode = languageCode, FieldName = "DefaultName", Value = value.Trim(), CreatedBy = CurrentUserService.GetCurrentUser().UserName }); return; }
        existing.Value = value.Trim(); Touch(existing);
    }

    // ─── Request DTOs ──────────────────────────────────────────
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
}
