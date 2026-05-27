using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class MoveLocationService : InventoryOperationBase, IInventoryOperationService
{
    private readonly IInventoryStatePolicy _statePolicy;

    public MoveLocationService(InventoryDbContext db, IInventoryStatePolicy statePolicy, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { _statePolicy = statePolicy; }

    public async Task<ServiceResult<PostedDocumentDto>> MoveLocationAsync(MoveLocationRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        // Resolve warehouse by code
        //var warehouse = await FindWarehouseByCodeAsync(request.WarehouseCode, cancellationToken);
        //if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse '{request.WarehouseCode}' not found.");
        //if (!user.CanAccessWarehouse(warehouse.Id))
        //    return ServiceResult<PostedDocumentDto>.Fail("Permission denied for move warehouse.");

        var warehouse = await FindWarehouseByIDAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse {request.WarehouseCode} not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for move warehouse.");

        //var errors = await ValidateMoveLocationAsync(request, warehouse, cancellationToken);
        //if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var lines = request.Lines.Where(x => !string.IsNullOrWhiteSpace(x.ItemCode) && !string.IsNullOrWhiteSpace(x.SerialNumber)).ToArray();
        if (!lines.Any()) return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");

        await using var transaction = await BeginOperationTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;

        var document = new MoveDocument
        {
            DocumentNo = string.IsNullOrWhiteSpace(request.DocumentNo) ? _documentNumbers.Next("MOV", request.DocumentDate) : request.DocumentNo.Trim(),
            DocumentDate = request.DocumentDate,
            WarehouseId = warehouse.Id, Note = request.Note,
            CreatedAt = now, CreatedBy = user.UserName, ApprovedBy = user.UserName, ApprovedAt = now, PostedAt = now
        };
        _db.MoveDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var preparedLines = new List<(MoveLocationLineRequest Line, ItemInstance Instance, CurrentItemLocation Current, BinLocation TargetBin, int? FromBin, string FromDisplay)>();
        var processedInstances = new HashSet<int>();
        var targetBinsInRequest = new HashSet<int>();

        foreach (var line in lines)
        {
            // Resolve instance by code
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
            if (!processedInstances.Add(instance.Id))
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is already used in another line.");
            if (!_statePolicy.CanMove(instance.Status)) return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not InStock.");

            // Resolve target bin by code
            var targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
            if (targetBin == null) return ServiceResult<PostedDocumentDto>.Fail($"BinCode {line.TargetBinCode} not found.");
            if (targetBin.WarehouseId != warehouse.Id) return ServiceResult<PostedDocumentDto>.Fail($"BinCode {line.TargetBinCode} does not belong to warehouse {warehouse.WarehouseCode}.");
            if (!targetBinsInRequest.Add(targetBin.Id)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");

            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            if (!current.WarehouseId.HasValue || current.WarehouseId.Value != warehouse.Id || !current.BinLocationId.HasValue)
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} does not belong to selected warehouse.");

            preparedLines.Add((line, instance, current, targetBin, current.BinLocationId, LocationDisplay(current)));
        }

        // Check target bins are not occupied by other items not in this move
        var movingItemIds = preparedLines.Select(x => x.Instance.Id).ToHashSet();
        foreach (var prepared in preparedLines)
        {
            var occupant = await _db.CurrentItemLocations.AsNoTracking()
                .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
                .FirstOrDefaultAsync(x =>
                    x.BinLocationId == prepared.TargetBin.Id &&
                    x.ItemInstanceId != prepared.Instance.Id &&
                    x.ItemInstance != null && x.ItemInstance.IsActive &&
                    x.ItemInstance.Status != ItemStatus.Lost && x.ItemInstance.Status != ItemStatus.Disposed,
                    cancellationToken);
            if (occupant != null && !movingItemIds.Contains(occupant.ItemInstanceId))
            {
                var occupiedItem = occupant.ItemInstance?.SerialNumber ?? occupant.ItemInstance?.Item?.ItemCode ?? occupant.ItemInstanceId.ToString();
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {prepared.TargetBin.FullPath} is occupied by item {occupiedItem} that is not moved in this document.");
            }
        }

        foreach (var prepared in preparedLines)
        {
            var line = prepared.Line; var instance = prepared.Instance; var current = prepared.Current;
            current.LocationType = LocationType.BinLocation; current.WarehouseId = warehouse.Id;
            current.BinLocationId = prepared.TargetBin.Id; current.ExternalPartyId = null; current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(MoveDocument); current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo; current.UpdatedLocationAt = now; current.UpdatedLocationBy = user.UserName;

            _db.MoveDocumentLines.Add(new MoveDocumentLine
            {
                MoveDocumentId = document.Id, ItemInstanceId = instance.Id,
                FromBinLocationId = prepared.FromBin, TargetBinLocationId = prepared.TargetBin.Id,
                Note = line.Note, CreatedAt = now, CreatedBy = user.UserName
            });

            if (prepared.FromBin.HasValue && prepared.FromBin.Value != prepared.TargetBin.Id)
            {
                await ApplyStockDeltaAsync(warehouse.Id, prepared.FromBin, instance.ItemId, instance.Status, -1, user, cancellationToken);
                await ApplyStockDeltaAsync(warehouse.Id, prepared.TargetBin.Id, instance.ItemId, instance.Status, 1, user, cancellationToken);
            }

            AddHistory(instance.Id, MovementActionType.MoveLocation, LocationType.BinLocation, prepared.FromBin, prepared.FromDisplay, LocationType.BinLocation, prepared.TargetBin.Id, prepared.TargetBin.FullPath, instance.Status, instance.Status, nameof(MoveDocument), document.Id, document.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.Move, instance.ItemId, instance.Id, warehouse.Id, prepared.TargetBin.Id, 0, instance.Status, nameof(MoveDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("MoveLocation", nameof(MoveDocument), document.Id, document.DocumentNo, user, "Move posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(MoveDocument), document.Id, document.DocumentNo, now), "Move posted.");
    }

    private async Task<List<string>> ValidateMoveLocationAsync(MoveLocationRequest request, Warehouse warehouse, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var lines = request.Lines.Where(x => !string.IsNullOrWhiteSpace(x.ItemCode) && !string.IsNullOrWhiteSpace(x.SerialNumber)).ToArray();
        if (!lines.Any())
        {
            errors.Add("At least one item is required.");
            return errors;
        }
        foreach (var line in lines)
        {
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) errors.Add($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");

            // Resolve target bin by code
            var targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
            if (targetBin == null)
            {
                errors.Add($"Target bin {line.TargetBinCode} not found.");
                return errors;
            }
            if (targetBin.WarehouseId != warehouse.Id) errors.Add($"BinCode {line.TargetBinCode} does not belong to warehouse {warehouse.WarehouseCode}.");
            //if (!targetBinsInRequest.Add(targetBin.Id)) errors.Add($"Target bin '{targetBin.FullPath}' is already used in another line.");

            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            if (!current.WarehouseId.HasValue || current.WarehouseId.Value != warehouse.Id || !current.BinLocationId.HasValue)
                errors.Add($"Item instance {line.ItemCode}/{line.SerialNumber} does not belong to selected warehouse.");
        }
        return errors;
    }
}

