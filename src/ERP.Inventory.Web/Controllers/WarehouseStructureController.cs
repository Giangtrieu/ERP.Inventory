using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

/// <summary>Warehouse zone/rack/shelf/bin structure CRUD</summary>
[Route("[controller]")]
public sealed class WarehouseStructureController : ManagementBaseController
{
    public WarehouseStructureController(InventoryDbContext db, ICurrentUserService currentUserService)
        : base(db, currentUserService) { }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? warehouseId, [FromQuery] bool? isActive, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        if (pageSize != 0) pageSize = Math.Clamp(pageSize, 1, 500);

        var user = CurrentUserService.GetCurrentUser();

        var query = Db.BinLocations.AsNoTracking().AsQueryable();
        if (warehouseId.HasValue) query = query.Where(x =>x.WarehouseId == warehouseId.Value);

        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);

        if (!user.IsAdmin) query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.BinCode.Contains(key) || x.FullPath.Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);

        var dataQuery = query.OrderBy(x => x.FullPath)
            .Select(x => new
            {
                id = x.Id,
                warehouse = x.Warehouse != null ? x.Warehouse.WarehouseCode: string.Empty,
                zone = x.Shelf != null &&x.Shelf.Rack != null && x.Shelf.Rack.WarehouseZone != null ? x.Shelf.Rack.WarehouseZone.ZoneCode: string.Empty,
                rack = x.Shelf != null && x.Shelf.Rack != null? x.Shelf.Rack.RackCode: string.Empty,
                shelf = x.Shelf != null ? x.Shelf.ShelfCode : string.Empty,
                bin = x.BinCode,
                fullPath = x.FullPath,
                isActive = x.IsActive
            });

        if (pageSize != 0) dataQuery = dataQuery .Skip((page - 1) * pageSize).Take(pageSize);

        var rows = await dataQuery.ToArrayAsync(cancellationToken);
        return Json(new
        {
            data = rows,
            total,
            page,
            pageSize
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] WarehouseStructureRequest request, CancellationToken cancellationToken)
    {
        var validation = request.WarehouseId.HasValue
            ? ValidateRequired(("ZoneCode", request.ZoneCode), ("ZoneName", request.ZoneName), ("RackCode", request.RackCode), ("RackName", request.RackName), ("ShelfCode", request.ShelfCode), ("ShelfName", request.ShelfName))
            : ValidateRequired(("CompanyCode", request.CompanyCode), ("CompanyName", request.CompanyName), ("BranchCode", request.BranchCode), ("BranchName", request.BranchName), ("WarehouseCode", request.WarehouseCode), ("WarehouseName", request.WarehouseName), ("ZoneCode", request.ZoneCode), ("ZoneName", request.ZoneName), ("RackCode", request.RackCode), ("RackName", request.RackName), ("ShelfCode", request.ShelfCode), ("ShelfName", request.ShelfName));
        if (validation != null) return validation;

        var currentUser = CurrentUserService.GetCurrentUser();
        var existingWarehouse = request.WarehouseId.HasValue
            ? await Db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken)
            : await Db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken);
        if (request.WarehouseId.HasValue && existingWarehouse == null) return Json(new { success = false, message = "Warehouse is invalid." });
        if (!currentUser.IsAdmin && (existingWarehouse == null || !currentUser.CanAccessWarehouse(existingWarehouse.Id)))
            return Json(new { success = false, message = "Current role cannot manage this warehouse." });

        var userName = currentUser.UserName;
        Warehouse warehouse;
        if (request.WarehouseId.HasValue)
        {
            warehouse = await Db.Warehouses.FirstAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken);
        }
        else
        {
            var company = await Db.Companies.FirstOrDefaultAsync(x => x.Code == request.CompanyCode, cancellationToken)
                ?? Db.Companies.Add(new Company { Code = request.CompanyCode, Name = request.CompanyName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
            var branch = await Db.Branches.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == request.BranchCode, cancellationToken)
                ?? Db.Branches.Add(new Branch { CompanyId = company.Id, Code = request.BranchCode, Name = request.BranchName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
            warehouse = await Db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken)
                ?? Db.Warehouses.Add(new Warehouse { BranchId = branch.Id, WarehouseCode = request.WarehouseCode, Name = request.WarehouseName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
        }

        var zone = await Db.WarehouseZones.FirstOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.ZoneCode == request.ZoneCode, cancellationToken)
            ?? Db.WarehouseZones.Add(new WarehouseZone { WarehouseId = warehouse.Id, ZoneCode = request.ZoneCode, Name = request.ZoneName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);
        var rack = await Db.Racks.FirstOrDefaultAsync(x => x.WarehouseZoneId == zone.Id && x.RackCode == request.RackCode, cancellationToken)
            ?? Db.Racks.Add(new Rack { WarehouseZoneId = zone.Id, RackCode = request.RackCode, Name = request.RackName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);
        var shelf = await Db.Shelves.FirstOrDefaultAsync(x => x.RackId == rack.Id && x.ShelfCode == request.ShelfCode, cancellationToken)
            ?? Db.Shelves.Add(new Shelf { RackId = rack.Id, ShelfCode = request.ShelfCode, Name = request.ShelfName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);

        var binCode = BuildBinCode(warehouse.WarehouseCode, request.RackCode, request.ShelfCode, request.BinCode);
        if (await Db.BinLocations.AnyAsync(x => x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
            return Json(new { success = false, message = "Bin code already exists in warehouse." });

        var bin = Db.BinLocations.Add(new BinLocation
        {
            WarehouseId = warehouse.Id, ShelfId = shelf.Id, BinCode = binCode,
            FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}",
            IsActive = request.IsActive, CreatedBy = userName
        }).Entity;
        await Db.SaveChangesAsync(cancellationToken);
        AddAudit("Create", nameof(BinLocation), bin.Id, bin.BinCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, data = new { warehouseId = warehouse.Id, binLocationId = bin.Id, binCode = bin.BinCode } });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var row = await Db.BinLocations.AsNoTracking()
            .Include(x => x.Warehouse)!.ThenInclude(x => x!.Branch)!.ThenInclude(x => x!.Company)
            .Include(x => x.Shelf)!.ThenInclude(x => x!.Rack)!.ThenInclude(x => x!.WarehouseZone)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, WarehouseId = x.WarehouseId,
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
                x.BinCode, x.IsActive
            }).FirstOrDefaultAsync(cancellationToken);
        if (row == null) return NotFound();

        var user = CurrentUserService.GetCurrentUser();
        if (!user.CanAccessWarehouse(row.WarehouseId)) return Forbid();
        return Json(row);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] WarehouseStructureRequest request, CancellationToken cancellationToken)
    {
        var bin = await Db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (bin == null) return NotFound();
        var userContext = CurrentUserService.GetCurrentUser();
        if (!userContext.CanAccessWarehouse(bin.WarehouseId)) return Forbid();

        var validation = request.WarehouseId.HasValue
            ? ValidateRequired(("ZoneCode", request.ZoneCode), ("ZoneName", request.ZoneName), ("RackCode", request.RackCode), ("RackName", request.RackName), ("ShelfCode", request.ShelfCode), ("ShelfName", request.ShelfName))
            : ValidateRequired(("CompanyCode", request.CompanyCode), ("CompanyName", request.CompanyName), ("BranchCode", request.BranchCode), ("BranchName", request.BranchName), ("WarehouseCode", request.WarehouseCode), ("WarehouseName", request.WarehouseName), ("ZoneCode", request.ZoneCode), ("ZoneName", request.ZoneName), ("RackCode", request.RackCode), ("RackName", request.RackName), ("ShelfCode", request.ShelfCode), ("ShelfName", request.ShelfName));
        if (validation != null) return validation;

        var targetWarehouse = request.WarehouseId.HasValue
            ? await Db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken)
            : await Db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken);
        if (request.WarehouseId.HasValue && targetWarehouse == null) return Json(new { success = false, message = "Warehouse is invalid." });
        if (!userContext.IsAdmin && (targetWarehouse == null || !userContext.CanAccessWarehouse(targetWarehouse.Id)))
            return Json(new { success = false, message = "Current role cannot manage this warehouse." });

        var userName = userContext.UserName;
        Warehouse warehouse;
        if (request.WarehouseId.HasValue)
        {
            warehouse = await Db.Warehouses.FirstAsync(x => x.Id == request.WarehouseId.Value && x.IsActive, cancellationToken);
        }
        else
        {
            var company = await Db.Companies.FirstOrDefaultAsync(x => x.Code == request.CompanyCode, cancellationToken)
                ?? Db.Companies.Add(new Company { Code = request.CompanyCode, Name = request.CompanyName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
            var branch = await Db.Branches.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == request.BranchCode, cancellationToken)
                ?? Db.Branches.Add(new Branch { CompanyId = company.Id, Code = request.BranchCode, Name = request.BranchName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
            warehouse = await Db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == request.WarehouseCode, cancellationToken)
                ?? Db.Warehouses.Add(new Warehouse { BranchId = branch.Id, WarehouseCode = request.WarehouseCode, Name = request.WarehouseName, CreatedBy = userName }).Entity;
            await Db.SaveChangesAsync(cancellationToken);
        }
        var zone = await Db.WarehouseZones.FirstOrDefaultAsync(x => x.WarehouseId == warehouse.Id && x.ZoneCode == request.ZoneCode, cancellationToken)
            ?? Db.WarehouseZones.Add(new WarehouseZone { WarehouseId = warehouse.Id, ZoneCode = request.ZoneCode, Name = request.ZoneName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);
        var rack = await Db.Racks.FirstOrDefaultAsync(x => x.WarehouseZoneId == zone.Id && x.RackCode == request.RackCode, cancellationToken)
            ?? Db.Racks.Add(new Rack { WarehouseZoneId = zone.Id, RackCode = request.RackCode, Name = request.RackName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);
        var shelf = await Db.Shelves.FirstOrDefaultAsync(x => x.RackId == rack.Id && x.ShelfCode == request.ShelfCode, cancellationToken)
            ?? Db.Shelves.Add(new Shelf { RackId = rack.Id, ShelfCode = request.ShelfCode, Name = request.ShelfName, CreatedBy = userName }).Entity;
        await Db.SaveChangesAsync(cancellationToken);

        var binCode = BuildBinCode(warehouse.WarehouseCode, request.RackCode, request.ShelfCode, request.BinCode);
        if (await Db.BinLocations.AnyAsync(x => x.Id != id && x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
            return Json(new { success = false, message = "Bin code already exists in warehouse." });

        bin.WarehouseId = warehouse.Id; bin.ShelfId = shelf.Id; bin.BinCode = binCode;
        bin.FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}";
        bin.IsActive = request.IsActive;
        Touch(bin); AddAudit("Update", nameof(BinLocation), bin.Id, bin.BinCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, data = new { warehouseId = warehouse.Id, binLocationId = bin.Id, binCode = bin.BinCode } });
    }

    [HttpPost("{id:int}/Deactivate")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        if (!CurrentUserService.GetCurrentUser().CanAccessWarehouse(entity.WarehouseId)) return Forbid();
        entity.IsActive = false; Touch(entity); AddAudit("SoftDelete", nameof(BinLocation), entity.Id, entity.BinCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost("{id:int}/Restore")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        if (!CurrentUserService.GetCurrentUser().CanAccessWarehouse(entity.WarehouseId)) return Forbid();
        entity.IsActive = true; Touch(entity); AddAudit("Restore", nameof(BinLocation), entity.Id, entity.BinCode);
        await Db.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await Db.BinLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null) return NotFound();
        Db.BinLocations.Remove(entity); AddAudit("HardDelete", nameof(BinLocation), entity.Id, entity.BinCode);
        return await TrySaveDeleteAsync(cancellationToken);
    }

    private static string BuildBinCode(string warehouseCode, string rackCode, string shelfCode, string? requestedBinCode)
    {
        if (!string.IsNullOrWhiteSpace(requestedBinCode)) return requestedBinCode.Trim();
        static string Normalize(string value) => value.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        return string.Join("_", new[] { Normalize(warehouseCode), Normalize(rackCode), Normalize(shelfCode) }.Where(x => x.Length > 0));
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
}
