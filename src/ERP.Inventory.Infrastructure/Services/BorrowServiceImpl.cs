using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Application.Services;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class BorrowServiceImpl : InventoryOperationBase, IBorrowService
{
    private readonly IInventoryStatePolicy _statePolicy;

    public BorrowServiceImpl(InventoryDbContext db, IInventoryStatePolicy statePolicy, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { _statePolicy = statePolicy; }

    public async Task<ServiceResult<PostedDocumentDto>> LendAsync(BorrowLendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var requiredErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.DocumentNo)) requiredErrors.Add("Borrow document number is required.");
        //if (string.IsNullOrWhiteSpace(request.WarehouseCode)) requiredErrors.Add("Borrow warehouse is required.");
        if (string.IsNullOrWhiteSpace(request.Purpose)) requiredErrors.Add("Purpose is required.");
        if (string.IsNullOrWhiteSpace(request.BorrowDepartment)) requiredErrors.Add("Borrow department is required.");
        if (string.IsNullOrWhiteSpace(request.ApprovedBy)) requiredErrors.Add("Approver is required.");
        if (string.IsNullOrWhiteSpace(request.BorrowerPhone)) requiredErrors.Add("Phone is required.");
        if (string.IsNullOrWhiteSpace(request.DepartmentOwner)) requiredErrors.Add("Department owner is required.");
        if (string.IsNullOrWhiteSpace(request.Borrower)) requiredErrors.Add("Borrower is required.");
        if (!request.Lines.Any()) requiredErrors.Add("At least one item is required.");
        if (requiredErrors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(requiredErrors);

        var warehouse = await FindWarehouseByIDAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse {request.WarehouseCode} not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for inventory check warehouse.");

        var documentNo = request.DocumentNo.Trim();
        //if (borrower == null) return ServiceResult<PostedDocumentDto>.Fail($"Borrower {request.BorrowerCode} not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var parts = request.Borrower.Split('-', 2);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) return ServiceResult<PostedDocumentDto>.Fail($"Borrower {request.Borrower} not found.");
        request.BorrowerCode = parts[0].Trim();
        request.BorrowerName = parts.Length > 1 ? parts[1] : "";
        var borrower = await FindPartyByCodeAsync(request.BorrowerCode, ExternalPartyType.Borrower, cancellationToken);
        if (borrower == null)
        {
            borrower = new ExternalParty
            {
                PartyCode = request.BorrowerCode,
                Name = request.BorrowerName,
                PartyType = ExternalPartyType.Borrower,
                Phone = request.BorrowerPhone,
                CreatedBy = user.UserName
            };
            _db.ExternalParties.Add(borrower);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var now = _clock.UtcNow;
        var processedInstances = new HashSet<int>();

        var oldDocument = await FindBorrowLendDocumentByCodeAsync(request.DocumentNo.Trim(), cancellationToken);
        if(oldDocument == null)
        {
            var document = new BorrowDocument
            {
                DocumentNo = documentNo,
                DocumentDate = request.BorrowDate,
                BorrowerId = borrower.Id,
                DueDate = request.DueDate,
                Purpose = request.Purpose,
                BorrowDepartment = request.BorrowDepartment.Trim(),
                BorrowerPhone = request.BorrowerPhone.Trim(),
                DepartmentOwner = request.DepartmentOwner.Trim(),
                CreatedAt = request.BorrowDate,
                CreatedBy = user.UserName,
                ApprovedBy = request.ApprovedBy.Trim(),
                ApprovedAt = request.BorrowDate,
                PostedAt = request.BorrowDate
            };
            _db.BorrowDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in request.Lines)
            {
                var targetExternalLocation = NormalizeExternalLocation(line.TargetExternalLocation);
                if (targetExternalLocation == null) return ServiceResult<PostedDocumentDto>.Fail("Target external location is required for borrowed item.");

                // Resolve instance by code
                var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
                if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
                if (!processedInstances.Add(instance.Id))
                    return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is already used in another line.");
                if (!_statePolicy.CanLend(instance.Status))
                    return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} cannot be lent (status: {instance.Status}).");

                var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
                var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
                if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
                    return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemCode}/{line.SerialNumber}.");
                if (!fromWarehouseId.HasValue || fromWarehouseId.Value != warehouse.Id) return ServiceResult<PostedDocumentDto>.Fail("Borrowed item must belong to the selected warehouse.");
                if (!fromBinLocationId.HasValue) return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not located in a warehouse bin.");

                _db.BorrowDocumentLines.Add(new BorrowDocumentLine
                {
                    BorrowDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    FromBinLocationId = fromBinLocationId,
                    TargetExternalLocation = targetExternalLocation,
                    Note = line.Note,
                    CreatedAt = request.BorrowDate,
                    CreatedBy = user.UserName
                });



                var oldStatus = instance.Status; var fromDisplay = LocationDisplay(current);
                instance.Status = ItemStatus.LentOut;
                current.LocationType = LocationType.Borrower; current.WarehouseId = warehouse.Id;
                current.BinLocationId = null; current.ExternalPartyId = borrower.Id; current.ExternalLocationText = targetExternalLocation;
                current.ReferenceDocumentType = nameof(BorrowDocument); current.ReferenceDocumentId = document.Id;
                current.ReferenceDocumentNo = document.DocumentNo; current.UpdatedLocationAt = now; current.UpdatedLocationBy = user.UserName;

                if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                    await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);

                var toDisplay = ExternalLocationDisplay(borrower.Name, targetExternalLocation);
                AddHistory(instance.Id, MovementActionType.Lend, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.Borrower, borrower.Id, toDisplay, oldStatus, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, string.IsNullOrWhiteSpace(line.Note) ? request.Purpose : line.Note, user);
                AddInventoryTransaction(InventoryTransactionType.BorrowLend, instance.ItemId, instance.Id, fromWarehouseId, fromBinLocationId, -1, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, user);

                // Ghi BorrowDocumentLog - BorrowIssue
                _db.BorrowDocumentLogs.Add(new BorrowDocumentLog
                {
                    BorrowDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    Action = "BorrowIssue",
                    OldStatus = oldStatus.ToString(),
                    NewStatus = ItemStatus.LentOut.ToString(),
                    Borrower = $"{borrower.PartyCode}-{borrower.Name}",
                    BorrowDepartment = request.BorrowDepartment,
                    BorrowerPhone = request.BorrowerPhone,
                    DepartmentOwner = request.DepartmentOwner,
                    OldLocationText = fromDisplay,
                    NewLocationText = toDisplay,
                    PerformedBy = user.UserName,
                    Timestamp = request.BorrowDate,
                    Note = line.Note
                });
            }

            AddPostSideEffects("BorrowLend", nameof(BorrowDocument), document.Id, document.DocumentNo, user, "Borrow lend posted.");
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(BorrowDocument), document.Id, document.DocumentNo, now), "Borrow lend posted.");
        }

        foreach (var line in request.Lines)
        {
            var targetExternalLocation = NormalizeExternalLocation(line.TargetExternalLocation);
            if (targetExternalLocation == null) return ServiceResult<PostedDocumentDto>.Fail("Target external location is required for borrowed item.");

            // Resolve instance by code
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
            if (!processedInstances.Add(instance.Id))
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is already used in another line.");
            if (!_statePolicy.CanLend(instance.Status))
                return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} cannot be lent.");
            
            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value))
                return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item instance {line.ItemCode}/{line.SerialNumber}.");
            if (!fromWarehouseId.HasValue || fromWarehouseId.Value != warehouse.Id) return ServiceResult<PostedDocumentDto>.Fail("Borrowed item must belong to the selected warehouse.");
            if (!fromBinLocationId.HasValue) return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not located in a warehouse bin.");
            var existingLine = await _db.BorrowDocumentLines.FirstOrDefaultAsync(x => x.BorrowDocumentId == oldDocument.Id && x.ItemInstanceId == instance.Id && _statePolicy.CanLend(instance.Status), cancellationToken);
            if (existingLine != null)
            {
                existingLine.IsReturned = false; existingLine.ReturnCondition = null;
                existingLine.ReturnedAt = null; existingLine.Note = line.Note;
                existingLine.FromBinLocationId = fromBinLocationId;
                existingLine.TargetExternalLocation = targetExternalLocation;
            }
            else
            {
                _db.BorrowDocumentLines.Add(new BorrowDocumentLine
                {
                    BorrowDocumentId = oldDocument.Id,
                    ItemInstanceId = instance.Id,
                    FromBinLocationId = fromBinLocationId,
                    TargetExternalLocation = targetExternalLocation,
                    Note = line.Note,
                    CreatedAt = request.BorrowDate,
                    CreatedBy = user.UserName
                });
            }

            var oldStatus = instance.Status; var fromDisplay = LocationDisplay(current);
            instance.Status = ItemStatus.LentOut;
            current.LocationType = LocationType.Borrower; current.WarehouseId = warehouse.Id;
            current.BinLocationId = null; current.ExternalPartyId = borrower.Id; current.ExternalLocationText = targetExternalLocation;
            current.ReferenceDocumentType = nameof(BorrowDocument); current.ReferenceDocumentId = oldDocument.Id;
            current.ReferenceDocumentNo = oldDocument.DocumentNo; current.UpdatedLocationAt = request.BorrowDate; current.UpdatedLocationBy = user.UserName;

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);

            var toDisplay = ExternalLocationDisplay(borrower.Name, targetExternalLocation);
            AddHistory(instance.Id, MovementActionType.Lend, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.Borrower, borrower.Id, toDisplay, oldStatus, ItemStatus.LentOut, nameof(BorrowDocument), oldDocument.Id, oldDocument.DocumentNo, string.IsNullOrWhiteSpace(line.Note) ? request.Purpose : line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.BorrowLend, instance.ItemId, instance.Id, fromWarehouseId, fromBinLocationId, -1, ItemStatus.LentOut, nameof(BorrowDocument), oldDocument.Id, oldDocument.DocumentNo, user);

            // Ghi BorrowDocumentLog - BorrowIssue (bổ sung vào document cũ)
            _db.BorrowDocumentLogs.Add(new BorrowDocumentLog
            {
                BorrowDocumentId = oldDocument.Id,
                ItemInstanceId = instance.Id,
                Action = "BorrowIssue",
                OldStatus = oldStatus.ToString(),
                NewStatus = ItemStatus.LentOut.ToString(),
                Borrower = $"{borrower.PartyCode}-{borrower.Name}",
                BorrowDepartment = request.BorrowDepartment,
                BorrowerPhone = request.BorrowerPhone,
                DepartmentOwner = request.DepartmentOwner,
                OldLocationText = fromDisplay,
                NewLocationText = toDisplay,
                PerformedBy = user.UserName,
                Timestamp = request.BorrowDate,
                Note = line.Note
            });
        }

        AddPostSideEffects("BorrowLend", nameof(BorrowDocument), oldDocument.Id, oldDocument.DocumentNo, user, "Borrow lend posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(BorrowDocument), oldDocument.Id, oldDocument.DocumentNo, now), "Borrow lend posted.");
    }

    public async Task<ServiceResult<PostedDocumentDto>> ReturnAsync(BorrowReturnRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        BorrowDocument? document;
        if (request.BorrowDocumentId > 0)
        {
            document = await _db.BorrowDocuments.Include(x => x.Borrower).FirstOrDefaultAsync(x => x.Id == request.BorrowDocumentId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.BorrowDocumentNo))
        {
            var documentNo = request.BorrowDocumentNo.Trim().ToUpperInvariant();
            document = await _db.BorrowDocuments.Include(x => x.Borrower).FirstOrDefaultAsync(x => x.DocumentNo.ToUpper() == documentNo, cancellationToken);
        }
        else
        {
            return ServiceResult<PostedDocumentDto>.Fail("Borrow document ID or document number is required.");
        }
        if (document == null) return ServiceResult<PostedDocumentDto>.Fail("Borrow document not found.");
        var requiredErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.BorrowDepartment)) requiredErrors.Add("Borrow department is required.");
        if (string.IsNullOrWhiteSpace(request.ApprovedBy)) requiredErrors.Add("Approver is required.");
        if (string.IsNullOrWhiteSpace(request.BorrowerPhone)) requiredErrors.Add("Phone is required.");
        if (string.IsNullOrWhiteSpace(request.DepartmentOwner)) requiredErrors.Add("Department owner is required.");
        if (string.IsNullOrWhiteSpace(request.Returner)) requiredErrors.Add("Borrower is required.");
        if (!request.Lines.Any()) requiredErrors.Add("At least one item is required.");
        if (requiredErrors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(requiredErrors);

        var parts = request.Returner.Split('-', 2);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) return ServiceResult<PostedDocumentDto>.Fail($"Borrower {request.Returner} not found.");
        request.ReturnerCode = parts[0].Trim();
        request.ReturnerName = parts.Length > 1 ? parts[1] : "";
        var borrower = await FindPartyByCodeAsync(request.ReturnerCode, ExternalPartyType.Borrower, cancellationToken);
        if (borrower == null)
        {
            borrower = new ExternalParty
            {
                PartyCode = request.ReturnerCode,
                Name = request.ReturnerName,
                PartyType = ExternalPartyType.Borrower,
                Phone = request.BorrowerPhone,
                CreatedBy = user.UserName
            };
            _db.ExternalParties.Add(borrower);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var targetBinsInRequest = new HashSet<int>();

        foreach (var line in request.Lines)
        {
            // Resolve instance by code
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode} with serial {line.SerialNumber} not found.");
            if (instance.Status != ItemStatus.LentOut) return ServiceResult<PostedDocumentDto>.Fail($"Item instance {line.ItemCode}/{line.SerialNumber} is not lent out.");

            var targetStatus = _statePolicy.StatusAfterBorrowReturn(line.Condition);

            // Resolve target bin by code if provided
            BinLocation? targetBin = null;
            if (!string.IsNullOrWhiteSpace(line.TargetBinCode))
            {
                targetBin = await FindBinByCodeAsync(line.TargetBinCode, cancellationToken);
                if (targetBin == null) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {line.TargetBinCode} not found.");
            }

            if (InventoryStatePolicy.IsInWarehouse(targetStatus) && targetBin == null) return ServiceResult<PostedDocumentDto>.Fail("Target bin is required for returned or damaged item.");
            if (targetBin != null && !user.CanAccessWarehouse(targetBin.WarehouseId)) return ServiceResult<PostedDocumentDto>.Fail("Permission denied for target bin.");
            if (targetBin != null && await BinHasActiveItemAsync(targetBin.Id, instance.Id, cancellationToken)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.FullPath} already contains another active item.");
            if (targetBin != null && !targetBinsInRequest.Add(targetBin.Id)) return ServiceResult<PostedDocumentDto>.Fail($"Target bin {targetBin.BinCode} is already used in another line.");

            var current = await GetCurrentLocationAsync(instance.Id, cancellationToken);
            if (current.WarehouseId.HasValue && !user.CanAccessWarehouse(current.WarehouseId.Value)) return ServiceResult<PostedDocumentDto>.Fail($"Permission denied for item {line.ItemCode}/{line.SerialNumber}.");

            var fromWarehouseId = current.WarehouseId; var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplay(current); var oldStatus = instance.Status;
            instance.Status = targetStatus;
            current.LocationType = targetStatus == ItemStatus.Lost ? LocationType.Unknown : LocationType.BinLocation;
            current.WarehouseId = targetBin?.WarehouseId ?? fromWarehouseId; current.BinLocationId = targetBin?.Id;
            current.ExternalPartyId = null; current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(BorrowDocument); current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo; current.UpdatedLocationAt = now; current.UpdatedLocationBy = user.UserName;

            var borrowLine = await _db.BorrowDocumentLines.FirstOrDefaultAsync(x => x.BorrowDocumentId == document.Id && x.ItemInstanceId == instance.Id, cancellationToken);
            if (borrowLine != null)
            {
                borrowLine.IsReturned = true; borrowLine.ReturnCondition = line.Condition;
                borrowLine.ReturnedAt = request.ReturnDate; borrowLine.Note = line.Note;
                borrowLine.UpdatedAt = request.ReturnDate; borrowLine.UpdatedBy = user.UserName;
            } else return ServiceResult<PostedDocumentDto>.Fail($"Item {line.ItemCode}/{line.SerialNumber} is not included in borrow document {document.DocumentNo}.");

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
            if (targetBin != null)
                await ApplyStockDeltaAsync(targetBin.WarehouseId, targetBin.Id, instance.ItemId, targetStatus, 1, user, cancellationToken);

            AddHistory(instance.Id, MovementActionType.ReturnBorrowed, LocationType.Borrower, document.BorrowerId, fromDisplay, targetBin != null ? LocationType.BinLocation : LocationType.Unknown, targetBin?.Id, targetBin?.FullPath ?? "Lost", oldStatus, targetStatus, nameof(BorrowDocument), document.Id, document.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.BorrowReturn, instance.ItemId, instance.Id, targetBin?.WarehouseId, targetBin?.Id, targetBin == null ? 0 : 1, targetStatus, nameof(BorrowDocument), document.Id, document.DocumentNo, user);

            // Ghi BorrowDocumentLog - BorrowReturn
            _db.BorrowDocumentLogs.Add(new BorrowDocumentLog
            {
                BorrowDocumentId = document.Id,
                ItemInstanceId = instance.Id,
                Action = "BorrowReturn",
                OldStatus = oldStatus.ToString(),
                NewStatus = targetStatus.ToString(),
                Borrower = borrower != null ? $"{request.ReturnerCode}-{request.ReturnerName}" : null,
                BorrowDepartment = request.BorrowDepartment,
                BorrowerPhone = request.BorrowerPhone,
                DepartmentOwner = request.DepartmentOwner,
                OldLocationText = fromDisplay,
                NewLocationText = targetBin?.FullPath ?? "Lost",
                PerformedBy = user.UserName,
                Timestamp = request.ReturnDate,
                Note = line.Note
            });
        }

        AddPostSideEffects("BorrowReturn", nameof(BorrowDocument), document.Id, document.DocumentNo, user, "Borrow return posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(BorrowDocument), document.Id, document.DocumentNo, now), "Borrow return posted.");
    }
}
