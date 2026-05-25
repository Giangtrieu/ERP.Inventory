using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class RepairServiceImpl : InventoryOperationBase, IRepairService
{
    private readonly IInventoryStatePolicy _statePolicy;

    public RepairServiceImpl(InventoryDbContext db, IInventoryStatePolicy statePolicy, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { _statePolicy = statePolicy; }

    public async Task<ServiceResult<PostedDocumentDto>> SendToRepairAsync(RepairSendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!request.Lines.Any()) return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");

        // Resolve vendor by code
        if (string.IsNullOrWhiteSpace(request.RepairVendorCode)) return ServiceResult<PostedDocumentDto>.Fail("Repair vendor code is required.");
        var vendor = await FindPartyByCodeAsync(request.RepairVendorCode, ExternalPartyType.RepairVendor, cancellationToken);
        if (vendor == null)
        {
            vendor = new ExternalParty
            {
                PartyCode = request.RepairVendorCode,
                ContactName = request.RepairVendorCode,
                CreatedAt = request.SendDate,
                CreatedBy = user.UserName,
                Email = "",
                IsActive = true,
                Phone = "",
                PartyType = ExternalPartyType.RepairVendor,
                Name = request.RepairVendorCode,
            };
            _db.ExternalParties.Add(vendor);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;

        // ── Find-or-Create document (append pattern, same as BorrowLend) ──
        var normalizedDocNo = string.IsNullOrWhiteSpace(request.DocumentNo)
            ? string.Empty
            : request.DocumentNo.Trim().ToUpperInvariant();

        RepairDocument document;
        bool isNew;

        if (!string.IsNullOrEmpty(normalizedDocNo))
        {
            document = await _db.RepairDocuments
                .FirstOrDefaultAsync(x => x.DocumentNo == normalizedDocNo, cancellationToken)!;
            isNew = document == null;
        }
        else
        {
            document = null!;
            isNew = true;
        }

        if (isNew)
        {
            document = new RepairDocument
            {
                DocumentNo = string.IsNullOrEmpty(normalizedDocNo)
                    ? _documentNumbers.Next("REP", request.SendDate)
                    : normalizedDocNo,
                DocumentDate = request.SendDate,
                RepairVendorId = vendor.Id,
                ExpectedReturnDate = request.ExpectedReturnDate,
                Reason = request.Reason,
                CreatedAt = now, CreatedBy = user.UserName,
                ApprovedBy = user.UserName, ApprovedAt = request.SendDate, PostedAt = request.SendDate,
            };
            _db.RepairDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ── Validate: no duplicate ItemInstance in same document ──────
        var existingInstanceIdList = await _db.RepairDocumentLines
            .Where(x => x.RepairDocumentId == document.Id)
            .Select(x => x.ItemInstanceId)
            .ToListAsync(cancellationToken);
        var existingInstanceIds = new HashSet<int>(existingInstanceIdList);


        var processedInstances = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            var targetExternalLocation = NormalizeExternalLocation(line.TargetExternalLocation);
            if (targetExternalLocation == null) return ServiceResult<PostedDocumentDto>.Fail("Target external location is required for every repaired item.");

            // Resolve instance by ItemCode + SerialNumber
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item '{line.ItemCode}' with serial '{line.SerialNumber}' not found.");

            if (!processedInstances.Add(instance.Id))
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is duplicated in this request.");
            if (isNew)
            {
                if (existingInstanceIds.Contains(instance.Id))
                    return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is already in repair document {document.DocumentNo}.");
            }
           

            if (!_statePolicy.CanSendToRepair(instance.Status))
                return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode}/{line.SerialNumber} cannot be sent to repair.");

            var oldStatus = instance.Status;
            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item {line.ItemCode}/{line.SerialNumber}.");
            if (!fromWarehouseId.HasValue || !fromBinLocationId.HasValue)
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not located in a warehouse bin.");

            var existingLine = await _db.RepairDocumentLines.FirstOrDefaultAsync(x => x.RepairDocumentId == document.Id && x.ItemInstanceId == instance.Id && _statePolicy.CanSendToRepair(instance.Status), cancellationToken);
            if (existingLine != null)
            {
                existingLine.IsReturned = false;
                existingLine.RepairResultNote = line.Note;
                existingLine.FromBinLocationId = fromBinLocationId;
                existingLine.TargetExternalLocation = targetExternalLocation;

            }
            else
            {
                _db.RepairDocumentLines.Add(new RepairDocumentLine
                {
                    RepairDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    FromBinLocationId = fromBinLocationId,
                    TargetExternalLocation = targetExternalLocation,
                    RepairResultNote = line.Note,
                    CreatedAt = request.SendDate,
                    CreatedBy = user.UserName,
                    IsReturned = false,
                });
            }

            instance.Status = ItemStatus.Repairing;
            current.LocationType = LocationType.RepairVendor; current.WarehouseId = fromWarehouseId;
            current.BinLocationId = null; current.ExternalPartyId = vendor.Id;
            current.ExternalLocationText = targetExternalLocation;
            current.ReferenceDocumentType = nameof(RepairDocument); current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = request.SendDate; current.UpdatedLocationBy = user.UserName;

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);

            var toDisplay = ExternalLocationDisplay(vendor.Name, targetExternalLocation);
            AddHistory(instance.Id, MovementActionType.SendToRepair, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.RepairVendor, vendor.Id, toDisplay, oldStatus, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, string.IsNullOrWhiteSpace(line.Note) ? request.Reason : line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.RepairSend, instance.ItemId, instance.Id, fromWarehouseId, fromBinLocationId, -1, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user);

            _db.RepairDocumentLogs.Add(new RepairDocumentLog
            {
                RepairDocumentId = document.Id,
                ItemInstanceId = instance.Id,
                Action = "RepairSend",
                OldStatus = oldStatus.ToString(),
                NewStatus = ItemStatus.Repairing.ToString(),
                RepairVendorName = vendor.Name,
                ExternalLocation = targetExternalLocation,
                OldLocationText = fromDisplay,
                NewLocationText = toDisplay,
                PerformedBy = user.UserName,
                Timestamp = request.SendDate,
                Note = string.IsNullOrWhiteSpace(line.Note) ? request.Reason : line.Note
            });
        }

        AddPostSideEffects("SendToRepair", nameof(RepairDocument), document.Id, document.DocumentNo, user, "Repair send posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(RepairDocument), document.Id, document.DocumentNo, now), isNew ? "Repair send posted." : "Lines appended to existing repair document.");
    }


    /// <summary>
    /// Task 5: Each line now has its own RepairResult — supports multi-row repair receive from one send document.
    /// </summary>
    public async Task<ServiceResult<PostedDocumentDto>> ReceiveFromRepairAsync(RepairReceiveRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        // Resolve document by ID or by DocumentNo string
        RepairDocument? document;
        if (request.RepairDocumentId > 0)
        {
            document = await _db.RepairDocuments.Include(x => x.RepairVendor)
                .FirstOrDefaultAsync(x => x.Id == request.RepairDocumentId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.RepairDocumentNo))
        {
            var normalizedNo = request.RepairDocumentNo.Trim().ToUpperInvariant();
            document = await _db.RepairDocuments.Include(x => x.RepairVendor)
                .FirstOrDefaultAsync(x => x.DocumentNo.ToUpper() == normalizedNo, cancellationToken);
        }
        else
        {
            return ServiceResult<PostedDocumentDto>.Fail("Repair document ID or document number is required.");
        }

        if (document == null) return ServiceResult<PostedDocumentDto>.Fail("Repair document not found.");

        if (!request.Lines.Any()) return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        document.UpdatedAt = now; document.UpdatedBy = user.UserName;

        var processedInstances = new HashSet<int>(); var targetBinsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            // Resolve instance by code
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
            if (!processedInstances.Add(instance.Id))
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is already used in another line.");

            if (instance.Status != ItemStatus.Repairing)
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not repairing.");

            var repairLine = await _db.RepairDocumentLines.FirstOrDefaultAsync(x => x.RepairDocumentId == document.Id && x.ItemInstanceId == instance.Id, cancellationToken);
            if (repairLine == null) return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not part of this repair document.");

            // Per-line result determines new status
            var newStatus = _statePolicy.StatusAfterRepairReceive(line.Result);

            // Resolve target bin by code
            var targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
            if (targetBin == null) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} not found.");
            if (!user.CanAccessWarehouse(targetBin.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for target bin.");
            if (!targetBinsInRequest.Add(targetBin.Id)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
            if (await BinHasActiveItemAsync(targetBin.Id, instance.Id, cancellationToken))
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");

            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemCode}/{line.SerialNumber}.");

            var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current); var oldStatus = instance.Status;
            instance.Status = newStatus;
            current.LocationType = LocationType.BinLocation; current.WarehouseId = targetBin.WarehouseId;
            current.BinLocationId = targetBin.Id; current.ExternalPartyId = null; current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(RepairDocument); current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo; current.UpdatedLocationAt = now; current.UpdatedLocationBy = user.UserName;

            repairLine.TargetBinLocationId = targetBin.Id; repairLine.NewSerialNumber = line.NewSerialNumber;
            repairLine.RepairResultNote = line.Note ?? request.ResultNote;
            repairLine.UpdatedAt = now; repairLine.UpdatedBy = user.UserName;
            repairLine.IsReturned = true;

            // Set per-line result on document (last result wins for document-level)

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, newStatus, 1, user, cancellationToken);

            AddHistory(instance.Id, MovementActionType.ReceiveFromRepair, LocationType.RepairVendor, document.RepairVendorId, fromDisplay, LocationType.BinLocation, targetBin.Id, targetBin.FullPath, oldStatus, newStatus, nameof(RepairDocument), document.Id, document.DocumentNo, line.Note ?? request.ResultNote, user);
            AddInventoryTransaction(InventoryTransactionType.RepairReceive, instance.ItemId, instance.Id, targetBin.WarehouseId, targetBin.Id, 1, newStatus, nameof(RepairDocument), document.Id, document.DocumentNo, user);

            _db.RepairDocumentLogs.Add(new RepairDocumentLog
            {
                RepairDocumentId = document.Id,
                ItemInstanceId = instance.Id,
                Action = "RepairReceive",
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString(),
                RepairVendorName = document.RepairVendor?.Name,
                OldLocationText = fromDisplay,
                NewLocationText = targetBin.FullPath,
                RepairResultNote = line.Note ?? request.ResultNote,
                PerformedBy = user.UserName,
                Timestamp = now
            });
        }

        AddPostSideEffects("ReceiveFromRepair", nameof(RepairDocument), document.Id, document.DocumentNo, user, "Repair receive posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(RepairDocument), document.Id, document.DocumentNo, now), "Repair receive posted.");
    }
}
