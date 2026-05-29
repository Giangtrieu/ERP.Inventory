using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public interface IDocumentRollbackService
{
    Task RestoreLocationTrackedInstancesAsync(IEnumerable<int> instanceIds, CancellationToken cancellationToken);
}

public sealed class DocumentRollbackService : IDocumentRollbackService
{
    private readonly InventoryDbContext _db;
    private readonly IDateTimeProvider _clock;

    public DocumentRollbackService(InventoryDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RestoreLocationTrackedInstancesAsync(IEnumerable<int> instanceIds, CancellationToken cancellationToken)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var instances = await _db.ItemInstances.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var locationRows = await _db.CurrentItemLocations
            .Where(x => ids.Contains(x.ItemInstanceId))
            .OrderByDescending(x => x.UpdatedLocationAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var duplicateLocations = locationRows
            .GroupBy(x => x.ItemInstanceId)
            .SelectMany(g => g.Skip(1))
            .ToArray();
        if (duplicateLocations.Length > 0)
        {
            _db.CurrentItemLocations.RemoveRange(duplicateLocations);
        }

        var locations = locationRows
            .GroupBy(x => x.ItemInstanceId)
            .ToDictionary(x => x.Key, x => x.First());
        var latestHistoryIds = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId)
            .Select(g => g.OrderByDescending(x => x.PerformedAt).ThenByDescending(x => x.Id).Select(x => x.Id).FirstOrDefault())
            .ToArrayAsync(cancellationToken);
        var latestHistories = latestHistoryIds.Length == 0
            ? new Dictionary<int, ItemMovementHistory>()
            : await _db.ItemMovementHistories
                .Where(x => latestHistoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.ItemInstanceId, cancellationToken);
        var latestRows = latestHistories.Values.ToArray();
        var latestBinIds = latestRows
            .Where(x => x.ToLocationType == LocationType.BinLocation && x.ToLocationId.HasValue)
            .Select(x => x.ToLocationId!.Value)
            .Concat(
                latestRows
                    .Where(x => x.ToLocationType is LocationType.Borrower or LocationType.RepairVendor
                             && x.FromLocationId.HasValue)
                    .Select(x => x.FromLocationId!.Value)
            )
            .Distinct()
            .ToArray();
        var binWarehouseMap = await _db.BinLocations
            .Where(x => latestBinIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.WarehouseId, cancellationToken);
        var borrowPairs = latestRows
            .Where(x => x.ToLocationType == LocationType.Borrower)
            .Select(x => new { x.DocumentId, x.ItemInstanceId })
            .Distinct()
            .ToArray();
        var repairPairs = latestRows
            .Where(x => x.ToLocationType == LocationType.RepairVendor)
            .Select(x => new { x.DocumentId, x.ItemInstanceId })
            .Distinct()
            .ToArray();
        var borrowTexts = borrowPairs.Length == 0
            ? new Dictionary<string, string?>()
            : await _db.BorrowDocumentLines
                .Where(x => borrowPairs.Select(p => p.DocumentId).Contains(x.BorrowDocumentId) && borrowPairs.Select(p => p.ItemInstanceId).Contains(x.ItemInstanceId))
                .ToDictionaryAsync(x => $"{x.BorrowDocumentId}:{x.ItemInstanceId}", x => x.TargetExternalLocation, cancellationToken);
        var repairTexts = repairPairs.Length == 0
            ? new Dictionary<string, string?>()
            : await _db.RepairDocumentLines
                .Where(x => repairPairs.Select(p => p.DocumentId).Contains(x.RepairDocumentId) && repairPairs.Select(p => p.ItemInstanceId).Contains(x.ItemInstanceId))
                .ToDictionaryAsync(x => $"{x.RepairDocumentId}:{x.ItemInstanceId}", x => x.TargetExternalLocation, cancellationToken);

        foreach (var instanceId in ids)
        {
            if (!instances.TryGetValue(instanceId, out var instance))
            {
                continue;
            }

            if (!latestHistories.TryGetValue(instanceId, out var latest))
            {
                if (locations.TryGetValue(instanceId, out var existingLocation))
                {
                    _db.CurrentItemLocations.Remove(existingLocation);
                }

                instance.IsActive = false;
                instance.UpdatedAt = _clock.UtcNow;
                instance.UpdatedBy = "system";
                continue;
            }

            instance.Status = latest.NewStatus;
            instance.IsActive = latest.NewStatus != ItemStatus.Replacement && latest.NewStatus != ItemStatus.Disposed;
            instance.UpdatedAt = _clock.UtcNow;
            instance.UpdatedBy = "system";

            var binLocation = latest.ToLocationType == LocationType.BinLocation && latest.ToLocationId.HasValue
                ? await _db.BinLocations.FirstOrDefaultAsync(x => x.Id == latest.ToLocationId.Value, cancellationToken)
                : null;

            if (!locations.TryGetValue(instanceId, out var location))
            {
                location = new CurrentItemLocation
                {
                    ItemInstanceId = instanceId,
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = "system"
                };
                _db.CurrentItemLocations.Add(location);
            }

            location.LocationType = latest.ToLocationType ?? LocationType.Unknown;
            location.ReferenceDocumentType = latest.DocumentType;
            location.ReferenceDocumentId = latest.DocumentId;
            location.ReferenceDocumentNo = latest.DocumentNo;
            location.UpdatedLocationAt = latest.PerformedAt;
            location.UpdatedLocationBy = latest.PerformedBy;
            location.BinLocationId = latest.ToLocationType == LocationType.BinLocation ? latest.ToLocationId : null;
            location.BinLocation = binLocation;
            location.ExternalPartyId = latest.ToLocationType is LocationType.Borrower or LocationType.RepairVendor ? latest.ToLocationId : null;
            location.ExternalLocationText = latest.ToLocationType switch
            {
                LocationType.Borrower => borrowTexts.GetValueOrDefault($"{latest.DocumentId}:{latest.ItemInstanceId}"),
                LocationType.RepairVendor => repairTexts.GetValueOrDefault($"{latest.DocumentId}:{latest.ItemInstanceId}"),
                _ => null
            };

            if (location.BinLocationId.HasValue && binWarehouseMap.TryGetValue(location.BinLocationId.Value, out var warehouseId))
            {
                location.WarehouseId = warehouseId;
            }
            else if (latest.ToLocationType is LocationType.Borrower or LocationType.RepairVendor
                  && latest.FromLocationId.HasValue
                  && binWarehouseMap.TryGetValue(latest.FromLocationId.Value, out var fromWarehouseId))
            {
                location.WarehouseId = fromWarehouseId;
            }
            else
            {
                location.WarehouseId = null;
            }
        }
    }
}
