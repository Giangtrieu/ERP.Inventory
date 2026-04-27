using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly InventoryDbContext _db;

    public DashboardService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.CurrentItemLocations.AsNoTracking().Include(x => x.ItemInstance).AsQueryable();

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        var now = DateTime.Now;
        var total = await query.CountAsync(cancellationToken);
        var inStock = await query.CountAsync(x => x.ItemInstance!.Status == ItemStatus.InStock, cancellationToken);
        var repairing = await query.CountAsync(x => x.ItemInstance!.Status == ItemStatus.Repairing, cancellationToken);
        var lentOut = await query.CountAsync(x => x.ItemInstance!.Status == ItemStatus.LentOut, cancellationToken);
        var damagedOrLost = await query.CountAsync(x => x.ItemInstance!.Status == ItemStatus.Damaged || x.ItemInstance.Status == ItemStatus.Lost, cancellationToken);
        var overdueQuery = _db.BorrowDocumentLines.AsNoTracking()
            .Include(x => x.BorrowDocument)
            .Include(x => x.FromBinLocation)
            .Include(x => x.TargetBinLocation)
            .Where(x => !x.IsReturned && x.BorrowDocument != null && x.BorrowDocument.DueDate < now);

        if (warehouseId.HasValue)
        {
            overdueQuery = user.CanAccessWarehouse(warehouseId.Value)
                ? overdueQuery.Where(x =>
                    (x.TargetBinLocation != null && x.TargetBinLocation.WarehouseId == warehouseId.Value) ||
                    (x.TargetBinLocation == null && x.FromBinLocation != null && x.FromBinLocation.WarehouseId == warehouseId.Value))
                : overdueQuery.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            overdueQuery = overdueQuery.Where(x =>
                (x.TargetBinLocation != null && user.WarehouseIds.Contains(x.TargetBinLocation.WarehouseId)) ||
                (x.TargetBinLocation == null && x.FromBinLocation != null && user.WarehouseIds.Contains(x.FromBinLocation.WarehouseId)));
        }

        var overdue = await overdueQuery.CountAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            TotalItems = total,
            InStock = inStock,
            Repairing = repairing,
            LentOut = lentOut,
            OverdueReturn = overdue,
            DamagedOrLost = damagedOrLost
        };
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByWarehouseAsync(string? status, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        ItemStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var statusValue))
        {
            parsedStatus = statusValue;
        }

        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.ItemInstance)
            .Where(x => x.ItemInstance != null);

        if (parsedStatus.HasValue)
        {
            query = query.Where(x => x.ItemInstance!.Status == parsedStatus.Value);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        return await query.GroupBy(x => x.Warehouse != null ? x.Warehouse.Name : "Unknown")
            .Select(x => new ChartPointDto { Label = x.Key, Value = x.Count() })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByStatusAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)
            .Where(x => x.ItemInstance != null);

        if (warehouseId.HasValue && user.CanAccessWarehouse(warehouseId.Value))
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        return await query.GroupBy(x => x.ItemInstance!.Status)
            .Select(x => new ChartPointDto { Key = x.Key.ToString(), Label = x.Key.ToString(), Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetMovementTrendAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var fromDate = DateTime.Now.Date.AddDays(-days + 1);
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Where(x => x.PerformedAt >= fromDate);

        query = ApplyMovementWarehouseScope(query, warehouseId, user);

        var rows = await query
            .GroupBy(x => x.PerformedAt.Date)
            .Select(x => new { Date = x.Key, Count = x.Count() })
            .OrderBy(x => x.Date)
            .ToArrayAsync(cancellationToken);

        var byDate = rows.ToDictionary(x => x.Date.Date, x => x.Count);
        return Enumerable.Range(0, days)
            .Select(i => fromDate.AddDays(i))
            .Select(x => new ChartPointDto
            {
                Key = x.ToString("yyyy-MM-dd"),
                Label = x.ToString("dd/MM"),
                Value = byDate.TryGetValue(x.Date, out var count) ? count : 0
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetMovementByActionAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var fromDate = DateTime.Now.Date.AddDays(-days + 1);
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Where(x => x.PerformedAt >= fromDate);

        query = ApplyMovementWarehouseScope(query, warehouseId, user);

        return await query.GroupBy(x => x.ActionType)
            .Select(x => new ChartPointDto { Key = x.Key.ToString(), Label = x.Key.ToString(), Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetStockByCategoryAsync(int? warehouseId, string? status, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        ItemStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ItemStatus>(status, true, out var statusValue))
        {
            parsedStatus = statusValue;
        }

        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)!.ThenInclude(x => x!.Category)
            .Where(x => x.ItemInstance != null && x.ItemInstance.Item != null);

        query = ApplyCurrentLocationWarehouseScope(query, warehouseId, user);

        if (parsedStatus.HasValue)
        {
            query = query.Where(x => x.ItemInstance!.Status == parsedStatus.Value);
        }

        return await query
            .GroupBy(x => x.ItemInstance!.Item!.Category != null ? x.ItemInstance.Item.Category.CategoryCode : "Unknown")
            .Select(x => new ChartPointDto { Key = x.Key, Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetLocationUtilizationAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var bins = _db.BinLocations.AsNoTracking().Where(x => x.IsActive);
        if (warehouseId.HasValue)
        {
            bins = user.CanAccessWarehouse(warehouseId.Value)
                ? bins.Where(x => x.WarehouseId == warehouseId.Value)
                : bins.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            bins = bins.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        var total = await bins.CountAsync(cancellationToken);
        var occupied = await bins.CountAsync(x => _db.CurrentItemLocations.Any(c =>
            c.BinLocationId == x.Id &&
            c.ItemInstance != null &&
            c.ItemInstance.IsActive &&
            c.ItemInstance.Status != ItemStatus.Lost &&
            c.ItemInstance.Status != ItemStatus.Disposed), cancellationToken);
        var empty = Math.Max(0, total - occupied);

        return new[]
        {
            new ChartPointDto { Key = "OccupiedBins", Label = "Occupied bins", Value = occupied },
            new ChartPointDto { Key = "EmptyBins", Label = "Empty bins", Value = empty }
        };
    }

    public async Task<IReadOnlyCollection<ChartPointDto>> GetOverdueBorrowAgingAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var query = _db.BorrowDocumentLines.AsNoTracking()
            .Include(x => x.BorrowDocument)
            .Include(x => x.FromBinLocation)
            .Include(x => x.TargetBinLocation)
            .Where(x => !x.IsReturned && x.BorrowDocument != null && x.BorrowDocument.DueDate < now);

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x =>
                    (x.TargetBinLocation != null && x.TargetBinLocation.WarehouseId == warehouseId.Value) ||
                    (x.TargetBinLocation == null && x.FromBinLocation != null && x.FromBinLocation.WarehouseId == warehouseId.Value))
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x =>
                (x.TargetBinLocation != null && user.WarehouseIds.Contains(x.TargetBinLocation.WarehouseId)) ||
                (x.TargetBinLocation == null && x.FromBinLocation != null && user.WarehouseIds.Contains(x.FromBinLocation.WarehouseId)));
        }

        var rows = await query.Select(x => x.BorrowDocument!.DueDate).ToArrayAsync(cancellationToken);
        var oneToSeven = rows.Count(x => (now.Date - x.Date).TotalDays <= 7);
        var eightToThirty = rows.Count(x => (now.Date - x.Date).TotalDays > 7 && (now.Date - x.Date).TotalDays <= 30);
        var overThirty = rows.Count(x => (now.Date - x.Date).TotalDays > 30);

        return new[]
        {
            new ChartPointDto { Key = "1-7", Label = "1-7 days", Value = oneToSeven },
            new ChartPointDto { Key = "8-30", Label = "8-30 days", Value = eightToThirty },
            new ChartPointDto { Key = "30+", Label = "Over 30 days", Value = overThirty }
        };
    }

    private IQueryable<CurrentItemLocation> ApplyCurrentLocationWarehouseScope(IQueryable<CurrentItemLocation> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            return user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        return query;
    }

    private IQueryable<ItemMovementHistory> ApplyMovementWarehouseScope(IQueryable<ItemMovementHistory> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            if (!user.CanAccessWarehouse(warehouseId.Value))
            {
                return query.Where(x => false);
            }

            return ApplyMovementWarehouseIds(query, new[] { warehouseId.Value });
        }

        if (!user.IsAdmin)
        {
            var warehouseIds = user.WarehouseIds.ToArray();
            query = ApplyMovementWarehouseIds(query, warehouseIds);
        }

        return query;
    }

    private IQueryable<ItemMovementHistory> ApplyMovementWarehouseIds(IQueryable<ItemMovementHistory> query, IReadOnlyCollection<int> warehouseIds)
    {
        if (warehouseIds.Count == 0)
        {
            return query.Where(x => false);
        }

        return query.Where(x =>
            _db.BinLocations.Any(b =>
                warehouseIds.Contains(b.WarehouseId) &&
                ((x.FromLocationType == LocationType.BinLocation && x.FromLocationId == b.Id) ||
                 (x.ToLocationType == LocationType.BinLocation && x.ToLocationId == b.Id))) ||
            _db.CurrentItemLocations.Any(c =>
                c.ItemInstanceId == x.ItemInstanceId &&
                c.WarehouseId.HasValue &&
                warehouseIds.Contains(c.WarehouseId.Value)));
    }
}
