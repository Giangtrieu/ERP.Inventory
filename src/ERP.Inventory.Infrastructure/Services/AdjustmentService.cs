using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class AdjustmentService : InventoryOperationBase
{
    private readonly IInventoryStatePolicy _statePolicy;

    public AdjustmentService(InventoryDbContext db, IInventoryStatePolicy statePolicy, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { _statePolicy = statePolicy; }

    public async Task<ServiceResult<PostedDocumentDto>> AdjustAsync(AdjustmentRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {


        var warehouse = await FindWarehouseByIDAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse {request.WarehouseCode} not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for adjustment warehouse.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return ServiceResult<PostedDocumentDto>.Fail("Adjustment reason is required.");



        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var document = new AdjustmentDocument
        {
            DocumentNo = _documentNumbers.Next("ADJ", request.DocumentDate),
            DocumentDate = request.DocumentDate,
            WarehouseId = warehouse.Id,
            Reason = request.Reason,
            CreatedAt = now,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = now,
            PostedAt = now
        };
        _db.AdjustmentDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var targetBinsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Reason)) return ServiceResult<PostedDocumentDto>.Fail("Line adjustment reason is required.");
            if (line.NewStatus == ItemStatus.Replacement)
            {
                var result = await HandleReplacement(line, warehouse, document, user, now, cancellationToken);
                if (!result.Success) return result;
            }
            else
            {
                var result = await HandleNormalAdjustment(line, warehouse, document, user, now, cancellationToken);
                if (!result.Success) return result;
            }
           
        }

        AddPostSideEffects("Adjustment", nameof(AdjustmentDocument), document.Id, document.DocumentNo, user, "Adjustment posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(AdjustmentDocument), document.Id, document.DocumentNo, now), "Adjustment posted.");
    }

    private async Task<ServiceResult<PostedDocumentDto>> HandleNormalAdjustment(AdjustmentLineRequest line,Warehouse warehouse, AdjustmentDocument document,CurrentUserContext user,DateTime now,CancellationToken cancellationToken)
    {
        var targetBinsInRequest = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(line.Reason)) return ServiceResult<PostedDocumentDto>.Fail("Line adjustment reason is required.");

        // Resolve instance by code
        var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
        if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");

        var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);

        // Resolve target bin by code if provided
        BinLocation? targetBin = null;
        int? targetExternalPartyId = null;
        if (!string.IsNullOrWhiteSpace(line.TargetBinCode))
        {
            targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
            if (targetBin == null) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} not found.");
            if (targetBin.WarehouseId != warehouse.Id) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} does not belong to warehouse.");
            if (!targetBinsInRequest.Add(targetBin.Id)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
            if (await BinHasActiveItemAsync(targetBin.Id, instance.Id, cancellationToken)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");
        }

        if (!string.IsNullOrWhiteSpace(line.TargetExternalPartyCode))
        {
            var party = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.PartyCode == line.TargetExternalPartyCode.Trim() && x.IsActive, cancellationToken);
            if (party == null) return ServiceResult<PostedDocumentDto>.Fail($"External party {line.TargetExternalPartyCode} not found.");
            targetExternalPartyId = party.Id;
        }

        var oldStatus = instance.Status;
        var fromLocationType = current.LocationType; var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
        var fromDisplay = LocationDisplay(current);
        instance.Status = line.NewStatus;
        current.LocationType = targetExternalPartyId.HasValue ? LocationType.Borrower : (targetBin != null ? LocationType.BinLocation : LocationType.Unknown);
        current.WarehouseId = targetBin != null ? warehouse.Id : null;
        current.BinLocationId = targetBin?.Id; current.ExternalPartyId = targetExternalPartyId;
        current.ExternalLocationText = null;
        current.ReferenceDocumentType = nameof(AdjustmentDocument); current.ReferenceDocumentId = document.Id;
        current.ReferenceDocumentNo = document.DocumentNo; current.UpdatedLocationAt = now; current.UpdatedLocationBy = user.UserName;

        _db.AdjustmentDocumentLines.Add(new AdjustmentDocumentLine
        {
            AdjustmentDocumentId = document.Id,
            ItemInstanceId = instance.Id,
            OldStatus = oldStatus,
            NewStatus = line.NewStatus,
            FromBinLocationId = fromBinLocationId,
            TargetBinLocationId = targetBin?.Id,
            TargetExternalPartyId = targetExternalPartyId,
            Reason = line.Reason,
            CreatedAt = now,
            CreatedBy = user.UserName
        });

        if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
        if (targetBin != null)
            await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, line.NewStatus, 1, user, cancellationToken);

        AddHistory(instance.Id, MovementActionType.Adjustment, fromLocationType, fromBinLocationId, fromDisplay, current.LocationType, current.BinLocationId ?? current.ExternalPartyId, targetBin?.FullPath ?? "Adjusted location", oldStatus, line.NewStatus, nameof(AdjustmentDocument), document.Id, document.DocumentNo, line.Reason, user);
        AddInventoryTransaction(InventoryTransactionType.Adjustment, instance.ItemId, instance.Id, warehouse.Id, targetBin?.Id, 0, line.NewStatus, nameof(AdjustmentDocument), document.Id, document.DocumentNo, user);

        _db.AdjustmentDocumentLogs.Add(new AdjustmentDocumentLog
        {
            AdjustmentDocumentId = document.Id,
            ItemInstanceId = instance.Id,
            Action = "Adjust",
            OldStatus = oldStatus.ToString(),
            NewStatus = line.NewStatus.ToString(),
            OldLocationText = fromDisplay,
            NewLocationText = targetBin?.FullPath ?? (targetExternalPartyId.HasValue ? "External" : "Unknown"),
            Reason = line.Reason,
            PerformedBy = user.UserName,
            Timestamp = now
        });

        return ServiceResult<PostedDocumentDto>.Ok(null);
    }

    private async Task<ServiceResult<PostedDocumentDto>> HandleReplacement(AdjustmentLineRequest line,Warehouse warehouse,AdjustmentDocument document,CurrentUserContext user,DateTime now,CancellationToken ct)
    {
        // ===== OLD SERIAL =====
        var actualBin = (await FindBinByCodeAsync(line.TargetBinCode, ct))!;
        if (actualBin == null) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} not found.");
        if (actualBin.WarehouseId != warehouse.Id) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} does not belong to warehouse.");

        var oldInstance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, ct);
        if (oldInstance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");


        var oldLocation = await GetCurrentLocationAsync(oldInstance.Id, ct);
        var oldBinlocationId = oldLocation.BinLocationId;
        var fromOldDisplay = LocationDisplay(oldLocation);

        var oldStatus = oldInstance.Status;

        // STOCK OUT
        if (oldLocation.WarehouseId.HasValue && oldLocation.BinLocationId.HasValue) 
            await ApplyStockDeltaAsync(oldLocation.WarehouseId.Value,oldLocation.BinLocationId.Value,oldInstance.ItemId, oldStatus, -1, user,ct);


        // mark old disposed
        oldInstance.Status = ItemStatus.Replacement;
        oldInstance.IsActive = false;

        oldLocation.LocationType = LocationType.Disposed;
        oldLocation.WarehouseId = null;
        oldLocation.BinLocationId = null;
        oldLocation.ExternalLocationText = null;
        oldLocation.ReferenceDocumentType = nameof(AdjustmentDocument); 
        oldLocation.ReferenceDocumentId = document.Id;
        oldLocation.ReferenceDocumentNo = document.DocumentNo; oldLocation.UpdatedLocationAt = now; 
        oldLocation.UpdatedLocationBy = user.UserName;
        
        // ===== NEW SERIAL =====
        var newInstance = await FindInstanceByCodeAsync(line.ItemCode, line.NewSerialNumber, ct);

        if (newInstance == null)
        {
            var item = await FindItemByCodeAsync(line.ItemCode, ct);

            newInstance = new ItemInstance
            {
                ItemId = item.Id,
                SerialNumber = line.NewSerialNumber,
                Barcode = line.NewSerialNumber,
                Status = ItemStatus.Normal,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = user.UserName
            };

            _db.ItemInstances.Add(newInstance);
            await _db.SaveChangesAsync(ct);

            _db.CurrentItemLocations.Add(new CurrentItemLocation
            {
                ItemInstanceId = newInstance.Id,
                LocationType = LocationType.BinLocation,
                WarehouseId = warehouse.Id,
                BinLocationId = actualBin.Id,
                ReferenceDocumentType = nameof(AdjustmentDocument),
                ReferenceDocumentId = document.Id,
                ReferenceDocumentNo = document.DocumentNo,
                UpdatedLocationAt = now,
                UpdatedLocationBy = user.UserName,
                CreatedAt = now,
                CreatedBy = user.UserName
            });

            await _db.SaveChangesAsync(ct);
        }
        else
        {
            await ApplyStockDeltaAsync(actualBin.WarehouseId, actualBin.Id, newInstance.ItemId, ItemStatus.InStock, +1, user, ct);
        }


        var newLocation = await GetCurrentLocationAsync(newInstance.Id, ct);
        var fromNewDisplay = LocationDisplay(newLocation);

        _db.AdjustmentDocumentLines.AddRange(
            new AdjustmentDocumentLine
            {
                AdjustmentDocumentId = document.Id,
                ItemInstanceId = oldInstance.Id,
                OldStatus = oldStatus,
                NewStatus = ItemStatus.Replacement,
                FromBinLocationId = oldBinlocationId,
                TargetBinLocationId = null,
                TargetExternalPartyId = null,
                Reason = line.Reason,
                CreatedAt = now,
                CreatedBy = user.UserName
            },
            new AdjustmentDocumentLine
            {
                AdjustmentDocumentId = document.Id,
                ItemInstanceId = newInstance.Id,
                OldStatus = ItemStatus.Replacement,
                NewStatus = newInstance.Status,
                FromBinLocationId = null,
                TargetBinLocationId = actualBin?.Id,
                TargetExternalPartyId = null,
                Reason = line.Reason,
                CreatedAt = now,
                CreatedBy = user.UserName
            });


        // history
        AddHistory(oldInstance.Id, MovementActionType.Adjustment, LocationType.BinLocation, oldLocation.BinLocationId, fromOldDisplay, oldLocation.LocationType, null, "", oldStatus, ItemStatus.Replacement, nameof(AdjustmentDocument), document.Id, document.DocumentNo, line.Reason, user);
        AddHistory(newInstance.Id, MovementActionType.Adjustment, LocationType.Unknown, null, "Unknown", newLocation.LocationType, newLocation.BinLocationId, actualBin?.FullPath ?? "Adjusted location", newInstance.Status, ItemStatus.InStock, nameof(AdjustmentDocument), document.Id, document.DocumentNo, line.Reason, user);

        AddInventoryTransaction(InventoryTransactionType.Adjustment, oldInstance.ItemId, oldInstance.Id, warehouse.Id, oldLocation?.Id, 0, ItemStatus.Replacement, nameof(AdjustmentDocument), document.Id, document.DocumentNo, user);
        AddInventoryTransaction(InventoryTransactionType.Adjustment, newInstance.ItemId, newInstance.Id, warehouse.Id, newLocation?.Id, 0, ItemStatus.InStock, nameof(AdjustmentDocument), document.Id, document.DocumentNo, user);

        _db.AdjustmentDocumentLogs.Add(new AdjustmentDocumentLog
        {
            AdjustmentDocumentId = document.Id,
            ItemInstanceId = oldInstance.Id,
            Action = "Replace-Out",
            OldStatus = oldStatus.ToString(),
            NewStatus = ItemStatus.Replacement.ToString(),
            OldLocationText = fromOldDisplay,
            NewLocationText = "Disposed",
            Reason = line.Reason,
            PerformedBy = user.UserName,
            Timestamp = now
        });
        _db.AdjustmentDocumentLogs.Add(new AdjustmentDocumentLog
        {
            AdjustmentDocumentId = document.Id,
            ItemInstanceId = newInstance.Id,
            Action = "Replace-In",
            OldStatus = ItemStatus.Replacement.ToString(),
            NewStatus = ItemStatus.InStock.ToString(),
            OldLocationText = "Unknown",
            NewLocationText = actualBin?.FullPath ?? "Unknown",
            Reason = line.Reason,
            PerformedBy = user.UserName,
            Timestamp = now
        });

        return ServiceResult<PostedDocumentDto>.Ok(null);
    }
    private async Task<List<string>> ValidateAdjustAsync(AdjustmentRequest request, Warehouse warehouse, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var lines = request.Lines.ToArray();
        if (!lines.Any())
        {
            errors.Add("At least one item is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Reason)) errors.Add("Adjustment reason is required.");
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Reason)) errors.Add("Line adjustment reason is required.");

            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) errors.Add($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
            BinLocation? targetBin = null;
            if (!string.IsNullOrWhiteSpace(line.TargetBinCode))
            {
                targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
                if (targetBin == null) errors.Add($"BinCode {line.TargetBinCode} not found.");
                else if (targetBin.WarehouseId != warehouse.Id) errors.Add($"BinCode {line.TargetBinCode} does not belong to warehouse {warehouse.WarehouseCode}.");
            }

            if (!string.IsNullOrWhiteSpace(line.TargetExternalPartyCode))
            {
                var party = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.PartyCode == line.TargetExternalPartyCode.Trim() && x.IsActive, cancellationToken);
                if (party == null) errors.Add($"External party {line.TargetExternalPartyCode} not found.");
            }
        }
        return errors;
    }
}

