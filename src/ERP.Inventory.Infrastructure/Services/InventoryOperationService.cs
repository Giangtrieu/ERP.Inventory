using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class InventoryOperationService : IInboundService, IInventoryOperationService, IRepairService, IBorrowService
{
    private readonly InventoryDbContext _db;
    private readonly IInventoryStatePolicy _statePolicy;
    private readonly IDocumentNumberService _documentNumbers;

    public InventoryOperationService(InventoryDbContext db, IInventoryStatePolicy statePolicy, IDocumentNumberService documentNumbers)
    {
        _db = db;
        _statePolicy = statePolicy;
        _documentNumbers = documentNumbers;
    }

    public async Task<ServiceResult<PostedDocumentDto>> CreateInboundAsync(InboundRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for inbound warehouse.");
        }

        var errors = await ValidateInboundAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return ServiceResult<PostedDocumentDto>.Fail(errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var document = new InboundDocument
        {
            DocumentNo = _documentNumbers.Next("INB", request.DocumentDate),
            DocumentDate = request.DocumentDate,
            SourceExternalPartyId = request.SourceExternalPartyId,
            WarehouseId = request.WarehouseId,
            Note = request.Note,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.InboundDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var line in request.Lines)
        {
            var targetBin = await _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.BinLocationId, cancellationToken);
            var instance = new ItemInstance
            {
                ItemId = line.ItemId,
                SerialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim(),
                Barcode = string.IsNullOrWhiteSpace(line.Barcode) ? null : line.Barcode.Trim(),
                Status = ItemStatus.InStock,
                CreatedBy = user.UserName
            };

            _db.ItemInstances.Add(instance);
            await _db.SaveChangesAsync(cancellationToken);

            _db.InboundDocumentLines.Add(new InboundDocumentLine
            {
                InboundDocumentId = document.Id,
                ItemId = line.ItemId,
                ItemInstanceId = instance.Id,
                SerialNumber = instance.SerialNumber,
                Barcode = instance.Barcode,
                Quantity = 1,
                BinLocationId = line.BinLocationId,
                Condition = line.Condition,
                Note = line.Note,
                CreatedBy = user.UserName
            });

            _db.CurrentItemLocations.Add(new CurrentItemLocation
            {
                ItemInstanceId = instance.Id,
                LocationType = LocationType.BinLocation,
                WarehouseId = request.WarehouseId,
                BinLocationId = line.BinLocationId,
                ReferenceDocumentType = nameof(InboundDocument),
                ReferenceDocumentId = document.Id,
                ReferenceDocumentNo = document.DocumentNo,
                UpdatedLocationAt = DateTime.Now,
                UpdatedLocationBy = user.UserName,
                CreatedBy = user.UserName
            });

            await ApplyStockDeltaAsync(request.WarehouseId, line.BinLocationId, line.ItemId, ItemStatus.InStock, 1, user, cancellationToken);
            AddHistory(instance.Id, MovementActionType.Inbound, null, null, "Supplier", LocationType.BinLocation, line.BinLocationId, targetBin?.FullPath ?? $"Bin {line.BinLocationId}", ItemStatus.Reserved, ItemStatus.InStock, nameof(InboundDocument), document.Id, document.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.Inbound, line.ItemId, instance.Id, request.WarehouseId, line.BinLocationId, 1, ItemStatus.InStock, nameof(InboundDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("Inbound", nameof(InboundDocument), document.Id, document.DocumentNo, user, "Inbound posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InboundDocument), document.Id, document.DocumentNo), "Inbound posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> MoveLocationAsync(MoveLocationRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for move warehouse.");
        }

        var lines = request.Lines.Where(x => x.ItemInstanceId > 0 && x.TargetBinLocationId > 0).ToArray();
        if (!lines.Any())
        {
            return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var document = new MoveDocument
        {
            DocumentNo = _documentNumbers.Next("MOV", request.DocumentDate),
            DocumentDate = request.DocumentDate,
            WarehouseId = request.WarehouseId,
            Note = request.Note,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.MoveDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var preparedLines = new List<(MoveLocationLineRequest Line, ItemInstance Instance, CurrentItemLocation Current, BinLocation TargetBin, int? FromBin, string FromDisplay)>();
        var itemIdsInRequest = new HashSet<int>();
        var targetBinsInRequest = new HashSet<int>();
        foreach (var line in lines)
        {
            if (!itemIdsInRequest.Add(line.ItemInstanceId))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is already used in another line.");
            }

            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} not found.");
            }

            if (!_statePolicy.CanMove(instance.Status))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not InStock.");
            }

            var targetBin = await _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.TargetBinLocationId && x.WarehouseId == request.WarehouseId && x.IsActive, cancellationToken);
            if (targetBin == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinLocationId} is invalid.");
            }

            if (!targetBinsInRequest.Add(line.TargetBinLocationId))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
            }

            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            if (!current.WarehouseId.HasValue || current.WarehouseId.Value != request.WarehouseId || !current.BinLocationId.HasValue)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} does not belong to selected warehouse.");
            }

            var fromBin = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            preparedLines.Add((line, instance, current, targetBin, fromBin, fromDisplay));
        }

        var movingItemIds = preparedLines.Select(x => x.Line.ItemInstanceId).ToHashSet();
        foreach (var prepared in preparedLines)
        {
            var occupant = await _db.CurrentItemLocations.AsNoTracking()
                .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
                .FirstOrDefaultAsync(x =>
                    x.BinLocationId == prepared.Line.TargetBinLocationId &&
                    x.ItemInstanceId != prepared.Line.ItemInstanceId &&
                    x.ItemInstance != null &&
                    x.ItemInstance.IsActive &&
                    x.ItemInstance.Status != ItemStatus.Lost &&
                    x.ItemInstance.Status != ItemStatus.Disposed,
                    cancellationToken);

            if (occupant != null && !movingItemIds.Contains(occupant.ItemInstanceId))
            {
                var occupiedItem = occupant.ItemInstance?.SerialNumber ?? occupant.ItemInstance?.Barcode ?? occupant.ItemInstance?.Item?.ItemCode ?? occupant.ItemInstanceId.ToString();
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {prepared.TargetBin.FullPath} is occupied by item {occupiedItem} that is not moved in this document.");
            }
        }

        foreach (var prepared in preparedLines)
        {
            var line = prepared.Line;
            var instance = prepared.Instance;
            var current = prepared.Current;
            current.LocationType = LocationType.BinLocation;
            current.WarehouseId = request.WarehouseId;
            current.BinLocationId = line.TargetBinLocationId;
            current.ExternalPartyId = null;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(MoveDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            _db.MoveDocumentLines.Add(new MoveDocumentLine
            {
                MoveDocumentId = document.Id,
                ItemInstanceId = line.ItemInstanceId,
                FromBinLocationId = prepared.FromBin,
                TargetBinLocationId = line.TargetBinLocationId,
                Note = line.Note,
                CreatedBy = user.UserName
            });

            if (prepared.FromBin.HasValue && prepared.FromBin.Value != line.TargetBinLocationId)
            {
                await ApplyStockDeltaAsync(request.WarehouseId, prepared.FromBin, instance.ItemId, ItemStatus.InStock, -1, user, cancellationToken);
                await ApplyStockDeltaAsync(request.WarehouseId, line.TargetBinLocationId, instance.ItemId, ItemStatus.InStock, 1, user, cancellationToken);
            }

            AddHistory(line.ItemInstanceId, MovementActionType.MoveLocation, LocationType.BinLocation, prepared.FromBin, prepared.FromDisplay, LocationType.BinLocation, line.TargetBinLocationId, prepared.TargetBin.FullPath, ItemStatus.InStock, ItemStatus.InStock, nameof(MoveDocument), document.Id, document.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.Move, instance.ItemId, instance.Id, request.WarehouseId, line.TargetBinLocationId, 0, ItemStatus.InStock, nameof(MoveDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("MoveLocation", nameof(MoveDocument), document.Id, document.DocumentNo, user, "Move posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(MoveDocument), document.Id, document.DocumentNo), "Move posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> SendToRepairAsync(RepairSendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var lines = request.Lines.Any()
            ? request.Lines.Where(x => x.ItemInstanceId > 0).ToArray()
            : request.ItemInstanceIds.Where(x => x > 0).Select(x => new RepairSendLineRequest { ItemInstanceId = x }).ToArray();

        if (!lines.Any())
        {
            return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");
        }

        var vendor = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.RepairVendorId && x.PartyType == ExternalPartyType.RepairVendor && x.IsActive, cancellationToken);
        if (vendor == null)
        {
            return ServiceResult<PostedDocumentDto>.Fail("Repair vendor is invalid.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var document = new RepairDocument
        {
            DocumentNo = _documentNumbers.Next("REP", request.SendDate),
            DocumentDate = request.SendDate,
            RepairVendorId = request.RepairVendorId,
            ExpectedReturnDate = request.ExpectedReturnDate,
            Reason = request.Reason,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.RepairDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var itemIdsInRequest = new HashSet<int>();
        foreach (var line in lines)
        {
            if (!itemIdsInRequest.Add(line.ItemInstanceId))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is already used in another line.");
            }

            var targetExternalLocation = NormalizeExternalLocation(line.TargetExternalLocation);
            if (targetExternalLocation == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail("Target external location is required for every repaired item.");
            }

            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null || !_statePolicy.CanSendToRepair(instance.Status))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} cannot be sent to repair.");
            }

            var oldStatus = instance.Status;
            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemInstanceId}.");
            }

            if (!fromWarehouseId.HasValue || !fromBinLocationId.HasValue)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not located in a warehouse bin.");
            }

            _db.RepairDocumentLines.Add(new RepairDocumentLine
            {
                RepairDocumentId = document.Id,
                ItemInstanceId = line.ItemInstanceId,
                FromBinLocationId = fromBinLocationId,
                TargetExternalLocation = targetExternalLocation,
                RepairResultNote = line.Note,
                CreatedBy = user.UserName
            });

            instance.Status = ItemStatus.Repairing;
            current.LocationType = LocationType.RepairVendor;
            current.WarehouseId = fromWarehouseId;
            current.BinLocationId = null;
            current.ExternalPartyId = request.RepairVendorId;
            current.ExternalLocationText = targetExternalLocation;
            current.ReferenceDocumentType = nameof(RepairDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            }

            var toDisplay = ExternalLocationDisplay(vendor.Name, targetExternalLocation);
            AddHistory(line.ItemInstanceId, MovementActionType.SendToRepair, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.RepairVendor, request.RepairVendorId, toDisplay, oldStatus, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, string.IsNullOrWhiteSpace(line.Note) ? request.Reason : line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.RepairSend, instance.ItemId, instance.Id, fromWarehouseId, fromBinLocationId, -1, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("SendToRepair", nameof(RepairDocument), document.Id, document.DocumentNo, user, "Repair send posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(RepairDocument), document.Id, document.DocumentNo), "Repair send posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> ReceiveFromRepairAsync(RepairReceiveRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var document = await _db.RepairDocuments.Include(x => x.RepairVendor).FirstOrDefaultAsync(x => x.Id == request.RepairDocumentId, cancellationToken);
        if (document == null)
        {
            return ServiceResult<PostedDocumentDto>.Fail("Repair document not found.");
        }

        var newStatus = _statePolicy.StatusAfterRepairReceive(request.Result);
        if (!request.Lines.Any())
        {
            return ServiceResult<PostedDocumentDto>.Fail("At least one item is required.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        document.ReceiveResult = request.Result;
        document.UpdatedAt = DateTime.Now;
        document.UpdatedBy = user.UserName;

        var itemIdsInRequest = new HashSet<int>();
        var targetBinsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            if (!itemIdsInRequest.Add(line.ItemInstanceId))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is already used in another line.");
            }

            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null || instance.Status != ItemStatus.Repairing)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not repairing.");
            }

            var repairLine = await _db.RepairDocumentLines.FirstOrDefaultAsync(x => x.RepairDocumentId == document.Id && x.ItemInstanceId == line.ItemInstanceId, cancellationToken);
            if (repairLine == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not part of this repair document.");
            }

            var targetBinId = line.TargetBinLocationId ?? request.TargetBinLocationId;
            if (!targetBinId.HasValue)
            {
                return ServiceResult<PostedDocumentDto>.Fail("Target bin is required when item returns to stock.");
            }

            var targetBin = await _db.BinLocations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetBinId.Value && x.IsActive, cancellationToken);
            if (targetBin == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBinId.Value} is invalid.");
            }

            if (!user.CanAccessWarehouse(targetBin.WarehouseId))
            {
                return ServiceResult<PostedDocumentDto>.Fail("Permission denied for target bin.");
            }

            if (!targetBinsInRequest.Add(targetBin.Id))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
            }

            if (await BinHasActiveItemAsync(targetBin.Id, line.ItemInstanceId, cancellationToken))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");
            }

            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemInstanceId}.");
            }

            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            var oldStatus = instance.Status;
            instance.Status = newStatus;
            current.LocationType = LocationType.BinLocation;
            current.WarehouseId = targetBin.WarehouseId;
            current.BinLocationId = targetBin.Id;
            current.ExternalPartyId = null;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(RepairDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            repairLine.TargetBinLocationId = targetBin.Id;
            repairLine.NewSerialNumber = line.NewSerialNumber;
            repairLine.RepairResultNote = request.ResultNote;
            repairLine.UpdatedAt = DateTime.Now;
            repairLine.UpdatedBy = user.UserName;

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            }

            await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, newStatus, 1, user, cancellationToken);

            AddHistory(line.ItemInstanceId, MovementActionType.ReceiveFromRepair, LocationType.RepairVendor, document.RepairVendorId, fromDisplay, LocationType.BinLocation, targetBin.Id, targetBin.FullPath, oldStatus, newStatus, nameof(RepairDocument), document.Id, document.DocumentNo, request.ResultNote, user);
            AddInventoryTransaction(InventoryTransactionType.RepairReceive, instance.ItemId, instance.Id, targetBin.WarehouseId, targetBin.Id, 1, newStatus, nameof(RepairDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("ReceiveFromRepair", nameof(RepairDocument), document.Id, document.DocumentNo, user, "Repair receive posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(RepairDocument), document.Id, document.DocumentNo), "Repair receive posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> LendAsync(BorrowLendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var lines = request.Lines.Any()
            ? request.Lines.Where(x => x.ItemInstanceId > 0).ToArray()
            : request.ItemInstanceIds.Where(x => x > 0).Select(x => new BorrowLendLineRequest { ItemInstanceId = x }).ToArray();

        var requiredErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.DocumentNo)) requiredErrors.Add("Borrow document number is required.");
        if (request.WarehouseId <= 0) requiredErrors.Add("Borrow warehouse is required.");
        if (string.IsNullOrWhiteSpace(request.Purpose)) requiredErrors.Add("Purpose is required.");
        if (string.IsNullOrWhiteSpace(request.BorrowDepartment)) requiredErrors.Add("Borrow department is required.");
        if (string.IsNullOrWhiteSpace(request.ApprovedBy)) requiredErrors.Add("Approver is required.");
        if (string.IsNullOrWhiteSpace(request.BorrowerPhone)) requiredErrors.Add("Phone is required.");
        if (string.IsNullOrWhiteSpace(request.DepartmentOwner)) requiredErrors.Add("Department owner is required.");
        if (!lines.Any()) requiredErrors.Add("At least one item is required.");
        if (requiredErrors.Count > 0)
        {
            return ServiceResult<PostedDocumentDto>.Fail(requiredErrors);
        }

        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for borrow warehouse.");
        }

        var documentNo = request.DocumentNo.Trim();
        if (await _db.BorrowDocuments.AnyAsync(x => x.DocumentNo == documentNo, cancellationToken))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Borrow document number already exists.");
        }

        var borrower = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.BorrowerId && x.IsActive, cancellationToken);
        if (borrower == null)
        {
            return ServiceResult<PostedDocumentDto>.Fail("Borrower is invalid.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var document = new BorrowDocument
        {
            DocumentNo = documentNo,
            DocumentDate = request.BorrowDate,
            BorrowerId = request.BorrowerId,
            DueDate = request.DueDate,
            Purpose = request.Purpose,
            BorrowDepartment = request.BorrowDepartment.Trim(),
            BorrowerPhone = request.BorrowerPhone.Trim(),
            DepartmentOwner = request.DepartmentOwner.Trim(),
            CreatedBy = user.UserName,
            ApprovedBy = request.ApprovedBy.Trim(),
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.BorrowDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var itemIdsInRequest = new HashSet<int>();
        foreach (var line in lines)
        {
            if (!itemIdsInRequest.Add(line.ItemInstanceId))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is already used in another line.");
            }

            var targetExternalLocation = NormalizeExternalLocation(line.TargetExternalLocation);
            if (targetExternalLocation == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail("Target external location is required for borrowed item.");
            }

            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null || !_statePolicy.CanLend(instance.Status))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} cannot be lent.");
            }

            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemInstanceId}.");
            }

            if (!fromWarehouseId.HasValue || fromWarehouseId.Value != request.WarehouseId)
            {
                return ServiceResult<PostedDocumentDto>.Fail("Borrowed item must belong to the selected warehouse.");
            }

            if (!fromBinLocationId.HasValue)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not located in a warehouse bin.");
            }

            _db.BorrowDocumentLines.Add(new BorrowDocumentLine
            {
                BorrowDocumentId = document.Id,
                ItemInstanceId = line.ItemInstanceId,
                FromBinLocationId = fromBinLocationId,
                TargetExternalLocation = targetExternalLocation,
                Note = line.Note,
                CreatedBy = user.UserName
            });

            var oldStatus = instance.Status;
            var fromDisplay = LocationDisplay(current);
            instance.Status = ItemStatus.LentOut;
            current.LocationType = LocationType.Borrower;
            current.WarehouseId = request.WarehouseId;
            current.BinLocationId = null;
            current.ExternalPartyId = request.BorrowerId;
            current.ExternalLocationText = targetExternalLocation;
            current.ReferenceDocumentType = nameof(BorrowDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            }

            var toDisplay = ExternalLocationDisplay(borrower.Name, targetExternalLocation);
            AddHistory(line.ItemInstanceId, MovementActionType.Lend, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.Borrower, request.BorrowerId, toDisplay, oldStatus, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, string.IsNullOrWhiteSpace(line.Note) ? request.Purpose : line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.BorrowLend, instance.ItemId, instance.Id, fromWarehouseId, fromBinLocationId, -1, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("BorrowLend", nameof(BorrowDocument), document.Id, document.DocumentNo, user, "Borrow lend posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(BorrowDocument), document.Id, document.DocumentNo), "Borrow lend posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> ReturnAsync(BorrowReturnRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var document = await _db.BorrowDocuments.Include(x => x.Borrower).FirstOrDefaultAsync(x => x.Id == request.BorrowDocumentId, cancellationToken);
        if (document == null)
        {
            return ServiceResult<PostedDocumentDto>.Fail("Borrow document not found.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var targetBinsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null || instance.Status != ItemStatus.LentOut)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} is not lent out.");
            }

            var targetStatus = _statePolicy.StatusAfterBorrowReturn(line.Condition);
            if (targetStatus is ItemStatus.InStock or ItemStatus.Damaged)
            {
                if (!line.TargetBinLocationId.HasValue)
                {
                    return ServiceResult<PostedDocumentDto>.Fail("Target bin is required for returned or damaged item.");
                }
            }

            var targetBin = line.TargetBinLocationId.HasValue
                ? await _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.TargetBinLocationId.Value && x.IsActive, cancellationToken)
                : null;

            if (line.TargetBinLocationId.HasValue && targetBin == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail("Target bin is invalid.");
            }

            if (targetBin != null && !user.CanAccessWarehouse(targetBin.WarehouseId))
            {
                return ServiceResult<PostedDocumentDto>.Fail("Permission denied for target bin.");
            }

            if (targetBin != null && await BinHasActiveItemAsync(targetBin.Id, line.ItemInstanceId, cancellationToken))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");
            }

            if (targetBin != null && !targetBinsInRequest.Add(targetBin.Id))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
            }

            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemInstanceId}.");
            }

            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            var oldStatus = instance.Status;
            instance.Status = targetStatus;
            current.LocationType = targetStatus == ItemStatus.Lost ? LocationType.Unknown : LocationType.BinLocation;
            current.WarehouseId = targetBin?.WarehouseId ?? fromWarehouseId;
            current.BinLocationId = targetBin?.Id;
            current.ExternalPartyId = null;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(BorrowDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            var borrowLine = await _db.BorrowDocumentLines.FirstOrDefaultAsync(x => x.BorrowDocumentId == document.Id && x.ItemInstanceId == line.ItemInstanceId, cancellationToken);
            if (borrowLine != null)
            {
                borrowLine.IsReturned = true;
                borrowLine.ReturnCondition = line.Condition;
                borrowLine.ReturnedAt = request.ReturnDate;
                borrowLine.Note = line.Note;
                borrowLine.UpdatedAt = DateTime.Now;
                borrowLine.UpdatedBy = user.UserName;
            }

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            }

            if (targetBin != null)
            {
                await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, targetStatus, 1, user, cancellationToken);
            }

            AddHistory(line.ItemInstanceId, MovementActionType.ReturnBorrowed, LocationType.Borrower, document.BorrowerId, fromDisplay, targetBin != null ? LocationType.BinLocation : LocationType.Unknown, targetBin?.Id, targetBin?.FullPath ?? "Lost", oldStatus, targetStatus, nameof(BorrowDocument), document.Id, document.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.BorrowReturn, instance.ItemId, instance.Id, targetBin?.WarehouseId, targetBin?.Id, targetBin == null ? 0 : 1, targetStatus, nameof(BorrowDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("BorrowReturn", nameof(BorrowDocument), document.Id, document.DocumentNo, user, "Borrow return posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(BorrowDocument), document.Id, document.DocumentNo), "Borrow return posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> AdjustAsync(AdjustmentRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for adjustment warehouse.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Adjustment reason is required.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var document = new AdjustmentDocument
        {
            DocumentNo = _documentNumbers.Next("ADJ", request.DocumentDate),
            DocumentDate = request.DocumentDate,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.AdjustmentDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        var targetBinsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Reason))
            {
                return ServiceResult<PostedDocumentDto>.Fail("Line adjustment reason is required.");
            }

            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == line.ItemInstanceId, cancellationToken);
            if (instance == null)
            {
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemInstanceId} not found.");
            }

            var current = await GetCurrentLocationAsync(line.ItemInstanceId, cancellationToken);
            BinLocation? targetBin = null;
            if (line.TargetBinLocationId.HasValue)
            {
                targetBin = await _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.TargetBinLocationId.Value && x.WarehouseId == request.WarehouseId && x.IsActive, cancellationToken);
                if (targetBin == null)
                {
                    return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinLocationId} is invalid.");
                }

                if (!targetBinsInRequest.Add(line.TargetBinLocationId.Value))
                {
                    return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} is already used in another line.");
                }

                if (await BinHasActiveItemAsync(line.TargetBinLocationId.Value, line.ItemInstanceId, cancellationToken))
                {
                    return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");
                }
            }

            var oldStatus = instance.Status;
            var fromLocationType = current.LocationType;
            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current);
            instance.Status = line.NewStatus;
            current.LocationType = line.TargetExternalPartyId.HasValue ? LocationType.Borrower : (line.TargetBinLocationId.HasValue ? LocationType.BinLocation : LocationType.Unknown);
            current.WarehouseId = line.TargetBinLocationId.HasValue ? request.WarehouseId : null;
            current.BinLocationId = line.TargetBinLocationId;
            current.ExternalPartyId = line.TargetExternalPartyId;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(AdjustmentDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = DateTime.Now;
            current.UpdatedLocationBy = user.UserName;

            _db.AdjustmentDocumentLines.Add(new AdjustmentDocumentLine
            {
                AdjustmentDocumentId = document.Id,
                ItemInstanceId = line.ItemInstanceId,
                OldStatus = oldStatus,
                NewStatus = line.NewStatus,
                FromBinLocationId = fromBinLocationId,
                TargetBinLocationId = line.TargetBinLocationId,
                TargetExternalPartyId = line.TargetExternalPartyId,
                Reason = line.Reason,
                CreatedBy = user.UserName
            });

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            }

            if (targetBin != null)
            {
                await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, line.NewStatus, 1, user, cancellationToken);
            }

            AddHistory(line.ItemInstanceId, MovementActionType.Adjustment, fromLocationType, fromBinLocationId, fromDisplay, current.LocationType, current.BinLocationId ?? current.ExternalPartyId, targetBin?.FullPath ?? "Adjusted location", oldStatus, line.NewStatus, nameof(AdjustmentDocument), document.Id, document.DocumentNo, line.Reason, user);
            AddInventoryTransaction(InventoryTransactionType.Adjustment, instance.ItemId, instance.Id, request.WarehouseId, line.TargetBinLocationId, 0, line.NewStatus, nameof(AdjustmentDocument), document.Id, document.DocumentNo, user);
        }

        AddPostSideEffects("Adjustment", nameof(AdjustmentDocument), document.Id, document.DocumentNo, user, "Adjustment posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(AdjustmentDocument), document.Id, document.DocumentNo), "Adjustment posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> CreateInventoryCheckAsync(InventoryCheckRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for inventory check warehouse.");
        }

        var checkErrors = await ValidateInventoryCheckAsync(request, user, cancellationToken);
        if (checkErrors.Count > 0)
        {
            return ServiceResult<PostedDocumentDto>.Fail(checkErrors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var document = new InventoryCheckDocument
        {
            DocumentNo = _documentNumbers.Next("CHK", request.SessionDate),
            DocumentDate = request.SessionDate,
            WarehouseId = request.WarehouseId,
            CountMethod = request.CountMethod,
            ResponsibleStaff = request.ResponsibleStaff,
            CreatedBy = user.UserName,
            ApprovedBy = user.UserName,
            ApprovedAt = DateTime.Now,
            PostedAt = DateTime.Now
        };

        _db.InventoryCheckDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var line in request.Lines)
        {
            int? systemBinId = null;
            if (line.ItemInstanceId.HasValue)
            {
                var current = await _db.CurrentItemLocations.AsNoTracking().FirstOrDefaultAsync(x => x.ItemInstanceId == line.ItemInstanceId.Value, cancellationToken);
                systemBinId = current?.BinLocationId;
            }

            _db.InventoryCheckLines.Add(new InventoryCheckLine
            {
                InventoryCheckDocumentId = document.Id,
                ItemInstanceId = line.ItemInstanceId,
                SystemBinLocationId = systemBinId,
                ActualBinLocationId = line.ActualBinLocationId,
                Result = line.Result,
                Note = line.Note,
                CreatedBy = user.UserName
            });
        }

        AddPostSideEffects("InventoryCheck", nameof(InventoryCheckDocument), document.Id, document.DocumentNo, user, "Inventory check posted.");
        AddInventoryCheckResultNotification(document, request, user);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InventoryCheckDocument), document.Id, document.DocumentNo), "Inventory check posted.");
    }

    private async Task<List<string>> ValidateInventoryCheckAsync(InventoryCheckRequest request, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.CountMethod))
        {
            errors.Add("Count method is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ResponsibleStaff))
        {
            errors.Add("Responsible staff is required.");
        }

        if (!request.Lines.Any())
        {
            errors.Add("Inventory check requires at least one line.");
            return errors;
        }

        var itemIdsInRequest = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            if (line.Result == default)
            {
                errors.Add("Inventory check result is required.");
                continue;
            }

            var isExtra = line.Result == InventoryCheckLineResult.Extra;
            if (!isExtra && !line.ItemInstanceId.HasValue)
            {
                errors.Add("Item Instance is required.");
                continue;
            }

            if (line.ItemInstanceId.HasValue && !itemIdsInRequest.Add(line.ItemInstanceId.Value))
            {
                errors.Add($"Item instance {line.ItemInstanceId.Value} is already used in another line.");
                continue;
            }

            CurrentItemLocation? current = null;
            if (line.ItemInstanceId.HasValue)
            {
                current = await _db.CurrentItemLocations.AsNoTracking()
                    .Include(x => x.ItemInstance)
                    .FirstOrDefaultAsync(x => x.ItemInstanceId == line.ItemInstanceId.Value, cancellationToken);
                if (current == null || current.ItemInstance == null)
                {
                    errors.Add($"Item instance {line.ItemInstanceId.Value} not found.");
                    continue;
                }

                if (current.WarehouseId != request.WarehouseId || !user.CanAccessWarehouse(request.WarehouseId))
                {
                    errors.Add($"Item instance {line.ItemInstanceId.Value} does not belong to selected warehouse.");
                    continue;
                }
            }

            var needsActualBin = line.Result is InventoryCheckLineResult.Matched or InventoryCheckLineResult.WrongLocation or InventoryCheckLineResult.Damaged or InventoryCheckLineResult.Extra;
            if (needsActualBin && !line.ActualBinLocationId.HasValue)
            {
                errors.Add("Actual bin is required for this inventory check result.");
                continue;
            }

            if (line.ActualBinLocationId.HasValue)
            {
                var actualBin = await _db.BinLocations.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == line.ActualBinLocationId.Value && x.IsActive, cancellationToken);
                if (actualBin == null || actualBin.WarehouseId != request.WarehouseId)
                {
                    errors.Add($"Actual bin {line.ActualBinLocationId.Value} is invalid for warehouse {request.WarehouseId}.");
                    continue;
                }
            }

            if (line.Result == InventoryCheckLineResult.Matched && current?.BinLocationId != line.ActualBinLocationId)
            {
                errors.Add("Matched result requires actual bin equal to the system bin.");
            }

            if (line.Result == InventoryCheckLineResult.WrongLocation && current?.BinLocationId == line.ActualBinLocationId)
            {
                errors.Add("Wrong location result requires an actual bin different from the system bin.");
            }
        }

        return errors;
    }

    private async Task<List<string>> ValidateInboundAsync(InboundRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (!request.Lines.Any())
        {
            errors.Add("At least one inbound line is required.");
        }

        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var binIds = new HashSet<int>();

        foreach (var line in request.Lines)
        {
            if (line.Quantity != 1)
            {
                errors.Add("This implementation tracks one item instance per line; quantity must be 1.");
            }

            if (line.ItemId <= 0)
            {
                errors.Add("Item is required.");
                continue;
            }

            var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.ItemId && x.IsActive, cancellationToken);
            if (item == null)
            {
                errors.Add($"Item {line.ItemId} is invalid.");
                continue;
            }

            if (item.IsSerialManaged && string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                errors.Add($"Item {item.ItemCode} requires serial number.");
            }

            if (!string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                var serialNumber = line.SerialNumber.Trim();
                if (!serials.Add(serialNumber))
                {
                    errors.Add($"Serial {serialNumber} is duplicated in this inbound document.");
                }

                var serialExists = await _db.ItemInstances.AnyAsync(x => x.SerialNumber == serialNumber, cancellationToken);
                if (serialExists)
                {
                    errors.Add($"Serial {serialNumber} already exists.");
                }
            }

            if (!string.IsNullOrWhiteSpace(line.Barcode))
            {
                var barcode = line.Barcode.Trim();
                if (!barcodes.Add(barcode))
                {
                    errors.Add($"Barcode {barcode} is duplicated in this inbound document.");
                }

                var barcodeExists = await _db.ItemInstances.AnyAsync(x => x.Barcode == barcode, cancellationToken);
                if (barcodeExists)
                {
                    errors.Add($"Barcode {barcode} already exists.");
                }
            }

            var bin = await _db.BinLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == line.BinLocationId && x.WarehouseId == request.WarehouseId && x.IsActive, cancellationToken);
            if (bin == null)
            {
                errors.Add($"Bin {line.BinLocationId} is invalid for warehouse {request.WarehouseId}.");
            }
            else
            {
                if (!binIds.Add(line.BinLocationId))
                {
                    errors.Add($"Bin {bin.FullPath} is already used in another inbound line.");
                }

                if (await BinHasActiveItemAsync(line.BinLocationId, null, cancellationToken))
                {
                    errors.Add($"Bin {bin.FullPath} already contains another active item.");
                }
            }
        }

        return errors;
    }

    private async Task<bool> BinHasActiveItemAsync(int binLocationId, int? exceptItemInstanceId, CancellationToken cancellationToken)
    {
        return await _db.CurrentItemLocations.AsNoTracking()
            .AnyAsync(x =>
                x.BinLocationId == binLocationId &&
                (!exceptItemInstanceId.HasValue || x.ItemInstanceId != exceptItemInstanceId.Value) &&
                x.ItemInstance != null &&
                x.ItemInstance.IsActive &&
                x.ItemInstance.Status != ItemStatus.Lost &&
                x.ItemInstance.Status != ItemStatus.Disposed,
                cancellationToken);
    }

    private async Task<CurrentItemLocation> GetCurrentLocationAsync(int itemInstanceId, CancellationToken cancellationToken)
    {
        var current = await _db.CurrentItemLocations
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.ItemInstanceId == itemInstanceId, cancellationToken);
        if (current == null)
        {
            throw new InvalidOperationException($"Current location for item instance {itemInstanceId} does not exist.");
        }

        return current;
    }

    private static string LocationDisplay(CurrentItemLocation location)
    {
        if (location.BinLocation != null)
        {
            return location.BinLocation.FullPath;
        }

        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText))
        {
            return location.ExternalParty != null
                ? ExternalLocationDisplay(location.ExternalParty.Name, location.ExternalLocationText)
                : location.ExternalLocationText;
        }

        if (location.ExternalParty != null)
        {
            return location.ExternalParty.Name;
        }

        if (location.Warehouse != null)
        {
            return location.Warehouse.Name;
        }

        if (!string.IsNullOrWhiteSpace(location.ReferenceDocumentNo))
        {
            return $"{location.LocationType} / {location.ReferenceDocumentNo}";
        }

        return location.LocationType.ToString();
    }

    private static string? NormalizeExternalLocation(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ExternalLocationDisplay(string partyName, string externalLocation)
    {
        return string.IsNullOrWhiteSpace(partyName) ? externalLocation : $"{partyName} - {externalLocation}";
    }

    private async Task ApplyStockDeltaAsync(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var balance = await _db.StockBalances.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId &&
            x.BinLocationId == binLocationId &&
            x.ItemId == itemId &&
            x.Status == status, cancellationToken);

        if (balance == null)
        {
            balance = new StockBalance
            {
                WarehouseId = warehouseId,
                BinLocationId = binLocationId,
                ItemId = itemId,
                Status = status,
                Quantity = 0,
                CreatedBy = user.UserName
            };
            _db.StockBalances.Add(balance);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = DateTime.Now;
        balance.UpdatedBy = user.UserName;
    }

    private void AddHistory(int itemInstanceId, MovementActionType action, LocationType? fromType, int? fromId, string? fromDisplay, LocationType? toType, int? toId, string? toDisplay, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, string? note, CurrentUserContext user)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstanceId = itemInstanceId,
            ActionType = action,
            FromLocationType = fromType,
            FromLocationId = fromId,
            FromLocationDisplay = fromDisplay,
            ToLocationType = toType,
            ToLocationId = toId,
            ToLocationDisplay = toDisplay,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNo = documentNo,
            Note = note,
            PerformedAt = DateTime.Now,
            PerformedBy = user.UserName
        });
    }

    private void AddInventoryTransaction(InventoryTransactionType type, int itemId, int? itemInstanceId, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionType = type,
            ItemId = itemId,
            ItemInstanceId = itemInstanceId,
            WarehouseId = warehouseId,
            BinLocationId = binLocationId,
            QuantityDelta = quantityDelta,
            StatusAfter = statusAfter,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNo = documentNo,
            PostedAt = DateTime.Now,
            PostedBy = user.UserName
        });
    }

    private void AddPostSideEffects(string action, string entityName, int entityId, string documentNo, CurrentUserContext user, string message)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNo = documentNo,
            Result = "Success",
            CreatedAt = DateTime.Now
        });

        if (!string.IsNullOrWhiteSpace(user.UserId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.UserId,
                Title = NotifyText(user, "Document posted"),
                Message = $"{NotifyText(user, message)} {documentNo}",
                LinkUrl = $"/?screen=tracking&documentNo={Uri.EscapeDataString(documentNo)}",
                CreatedAt = DateTime.Now,
                CreatedBy = user.UserName
            });
        }
    }

    private void AddInventoryCheckResultNotification(InventoryCheckDocument document, InventoryCheckRequest request, CurrentUserContext user)
    {
        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            return;
        }

        var issueGroups = request.Lines
            .Where(x => x.Result != InventoryCheckLineResult.Matched)
            .GroupBy(x => x.Result)
            .Select(x => $"{NotifyText(user, $"InventoryCheck.{x.Key}")}: {x.Count()}")
            .ToArray();

        var hasIssues = issueGroups.Length > 0;
        var guidance = hasIssues
            ? string.Join(" ", request.Lines.Select(x => x.Result).Distinct().Where(x => x != InventoryCheckLineResult.Matched).Select(x => NotifyText(user, $"InventoryCheck.Guidance.{x}")))
            : NotifyText(user, "Inventory check completed without discrepancy.");

        _db.Notifications.Add(new Notification
        {
            UserId = user.UserId,
            Title = NotifyText(user, hasIssues ? "Inventory check action required" : "Inventory check result"),
            Message = hasIssues
                ? $"{NotifyText(user, "Inventory check completed with discrepancies.")} {string.Join("; ", issueGroups)}. {guidance} {document.DocumentNo}"
                : $"{guidance} {document.DocumentNo}",
            LinkUrl = "/?screen=inventory-check",
            CreatedAt = DateTime.Now,
            CreatedBy = user.UserName
        });
    }

    private static PostedDocumentDto ToPostedDto(string documentType, int documentId, string documentNo)
    {
        return new PostedDocumentDto
        {
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNo = documentNo,
            PostedAt = DateTime.Now
        };
    }

    private static string NotifyText(CurrentUserContext user, string key)
    {
        var language = user.LanguageCode?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };

        return NotificationResources.TryGetValue(language, out var resources) && resources.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> NotificationResources = new()
    {
        ["vi"] = new()
        {
            ["Document posted"] = "Chứng từ đã ghi sổ",
            ["Inbound posted."] = "Đã ghi sổ nhập kho.",
            ["Move posted."] = "Đã ghi sổ chuyển vị trí.",
            ["Repair send posted."] = "Đã ghi sổ gửi sửa chữa.",
            ["Repair receive posted."] = "Đã ghi sổ nhận sửa chữa.",
            ["Borrow lend posted."] = "Đã ghi sổ phiếu mượn.",
            ["Borrow return posted."] = "Đã ghi sổ phiếu trả.",
            ["Adjustment posted."] = "Đã ghi sổ điều chỉnh.",
            ["Inventory check posted."] = "Đã ghi sổ kiểm kê.",
            ["Inventory check result"] = "Kết quả kiểm kê",
            ["Inventory check action required"] = "Kiểm kê cần xử lý",
            ["Inventory check completed without discrepancy."] = "Kiểm kê hoàn tất, không phát hiện chênh lệch.",
            ["Inventory check completed with discrepancies."] = "Kiểm kê phát hiện chênh lệch.",
            ["InventoryCheck.Missing"] = "Thiếu hàng",
            ["InventoryCheck.Extra"] = "Thừa hàng",
            ["InventoryCheck.WrongLocation"] = "Sai vị trí",
            ["InventoryCheck.Damaged"] = "Hư hỏng",
            ["InventoryCheck.Guidance.Missing"] = "Thiếu hàng: kiểm tra lại khu vực, lịch sử xuất nhập và báo quản lý nếu không tìm thấy.",
            ["InventoryCheck.Guidance.Extra"] = "Thừa hàng: xác minh nguồn phát sinh rồi tạo nhập kho hoặc điều chỉnh nếu hợp lệ.",
            ["InventoryCheck.Guidance.WrongLocation"] = "Sai vị trí: đưa hàng về đúng vị trí hoặc tạo phiếu chuyển vị trí để đồng bộ hệ thống.",
            ["InventoryCheck.Guidance.Damaged"] = "Hư hỏng: cách ly hàng, tạo phiếu sửa chữa hoặc điều chỉnh trạng thái."
        },
        ["en"] = new()
        {
            ["Inventory check result"] = "Inventory check result",
            ["Inventory check action required"] = "Inventory check action required",
            ["Inventory check completed without discrepancy."] = "Inventory check completed without discrepancy.",
            ["Inventory check completed with discrepancies."] = "Inventory check completed with discrepancies.",
            ["InventoryCheck.Missing"] = "Missing",
            ["InventoryCheck.Extra"] = "Extra",
            ["InventoryCheck.WrongLocation"] = "Wrong location",
            ["InventoryCheck.Damaged"] = "Damaged",
            ["InventoryCheck.Guidance.Missing"] = "Missing: recheck the area and movement history, then escalate if the item is not found.",
            ["InventoryCheck.Guidance.Extra"] = "Extra: identify the source, then create inbound or adjustment if valid.",
            ["InventoryCheck.Guidance.WrongLocation"] = "Wrong location: move the item back physically or post a move document to align the system.",
            ["InventoryCheck.Guidance.Damaged"] = "Damaged: quarantine the item, then create repair or status adjustment."
        },
        ["zh"] = new()
        {
            ["Document posted"] = "单据已过账",
            ["Inbound posted."] = "入库已过账。",
            ["Move posted."] = "移库已过账。",
            ["Repair send posted."] = "送修已过账。",
            ["Repair receive posted."] = "维修入库已过账。",
            ["Borrow lend posted."] = "借出单已过账。",
            ["Borrow return posted."] = "归还单已过账。",
            ["Adjustment posted."] = "调整已过账。",
            ["Inventory check posted."] = "盘点已过账。",
            ["Inventory check result"] = "盘点结果",
            ["Inventory check action required"] = "盘点需处理",
            ["Inventory check completed without discrepancy."] = "盘点完成，未发现差异。",
            ["Inventory check completed with discrepancies."] = "盘点发现差异。",
            ["InventoryCheck.Missing"] = "缺失",
            ["InventoryCheck.Extra"] = "多出",
            ["InventoryCheck.WrongLocation"] = "位置错误",
            ["InventoryCheck.Damaged"] = "损坏",
            ["InventoryCheck.Guidance.Missing"] = "缺失：复查区域和出入库历史，未找到时上报管理人员。",
            ["InventoryCheck.Guidance.Extra"] = "多出：确认来源，合法时创建入库或调整。",
            ["InventoryCheck.Guidance.WrongLocation"] = "位置错误：将实物放回正确位置，或创建移库单同步系统。",
            ["InventoryCheck.Guidance.Damaged"] = "损坏：隔离物料，创建维修单或状态调整。"
        }
    };
}
