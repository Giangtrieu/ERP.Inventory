using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.Data.SqlClient.Server;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class InboundService : InventoryOperationBase, IInboundService
{
    public InboundService(InventoryDbContext db, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock) { }

    public async Task<ServiceResult<PostedDocumentDto>> CreateInboundAsync(InboundRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {

        var warehouse = await FindWarehouseByIDAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null) return ServiceResult<PostedDocumentDto>.Fail($"Warehouse {request.WarehouseCode} not found.");
        if (!user.CanAccessWarehouse(request.WarehouseId))
        {
            return ServiceResult<PostedDocumentDto>.Fail("Permission denied for inbound warehouse.");
        }
        // Resolve optional supplier by code
        var errors = await ValidateInboundAsync(request, warehouse, cancellationToken);
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // ── Resolve optional Supplier ────────────────────────────────
        int? sourcePartyId = null;
        if (!string.IsNullOrWhiteSpace(request.SourcePartyer))
        {
            var parts = request.SourcePartyer.Split('-', 2);
            request.SourcePartyCode = parts[0].Trim();
            request.SourcePartyName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(request.SourcePartyCode))
            {
                var supplier = await FindPartyByCodeAsync(request.SourcePartyCode, ExternalPartyType.Supplier, cancellationToken);
                if (supplier == null)
                {
                    supplier = new ExternalParty
                    {
                        PartyCode = request.SourcePartyCode,
                        Name = request.SourcePartyName,
                        PartyType = ExternalPartyType.Supplier,
                        Phone = request.ReceiverPhone,
                        CreatedBy = user.UserName
                    };
                    _db.ExternalParties.Add(supplier);
                    await _db.SaveChangesAsync(cancellationToken);
                }
                sourcePartyId = supplier.Id;
            }
        }

        // ── Resolve Receiver (người nhập kho) — optional ─────────────
        int? receiverId = null;
        string receiverDisplay = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.ReceiverCode))
        {
            if (!string.IsNullOrWhiteSpace(request.ReceiverName))
            {
                var receiver = await FindPartyByCodeAsync(request.ReceiverCode, ExternalPartyType.Receiver, cancellationToken);
                if (receiver == null)
                {
                    receiver = new ExternalParty
                    {
                        PartyCode = request.ReceiverCode,
                        Name = request.ReceiverName,
                        PartyType = ExternalPartyType.Receiver,
                        Phone = request.ReceiverPhone,
                        CreatedBy = user.UserName
                    };
                    _db.ExternalParties.Add(receiver);
                    await _db.SaveChangesAsync(cancellationToken);
                }
                receiverId = receiver.Id;
                receiverDisplay = $"{receiver.PartyCode}-{receiver.Name}";
            }
        }

        var now = _clock.UtcNow;

        var oldDocument = await FindInboundDocumentByCodeAsync(request.DocumentNo.Trim(), cancellationToken);

        if (oldDocument == null)
        {
            var document = new InboundDocument
            {
                DocumentNo = request.DocumentNo.Trim().ToLower() != "auto" ? request.DocumentNo.Trim() : _documentNumbers.Next("", DateTime.UtcNow),
                DocumentDate = request.DocumentDate,
                SourceExternalPartyId = sourcePartyId,
                ReceiverId = receiverId,
                PartyDepartment = request.ReceiverDepartment.Trim(),
                PartyPhone = request.ReceiverPhone.Trim(),
                DepartmentOwner = request.DepartmentOwner.Trim(),
                WarehouseId = warehouse.Id,
                Note = request.Note,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName,
                ApprovedBy = request.ApprovedBy.Trim().Length > 0 ? request.ApprovedBy.Trim() : user.UserName,
                ApprovedAt = request.DocumentDate,
                PostedAt = request.DocumentDate
            };
            _db.InboundDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var line in request.Lines)
            {
                //var item = await FindItemByCodeAsync(line.ItemCode, cancellationToken);
                var item = await FindItemByCodeAsync(line.ItemCode, cancellationToken);
                if (item == null)
                {
                    var itemCategory = await FindItemCategoryByCodeAsync("GB300", cancellationToken);
                    var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == "PCS", cancellationToken);
                    item = new Item
                    {
                        ItemCode = line.ItemCode,
                        DefaultName = line.ItemCode.ToString(),
                        CategoryId = itemCategory == null? 0 : itemCategory.Id,
                        CreatedAt = request.DocumentDate,
                        IsActive = true,
                        UnitId = unit == null ? 0 : unit.Id,
                        IsSerialManaged = true,

                    };
                    _db.Items.Add(item);
                    await _db.SaveChangesAsync(cancellationToken);
                }
                var bin = await FindBinByCodeAsync(line.BinCode, cancellationToken);
                var serialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim();

                var instance = new ItemInstance
                {
                    ItemId = item!.Id,
                    SerialNumber = serialNumber,
                    Barcode = serialNumber, // Barcode = SerialNumber
                    MT = line.MT,
                    DocumentNo = document.DocumentNo,
                    Status = ResolveInboundStatus(line.Condition),
                    TrackingType = ItemTrackingType.LocationTracked,
                    OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName,
                };
                _db.ItemInstances.Add(instance);
                await _db.SaveChangesAsync(cancellationToken);

                _db.InboundDocumentLines.Add(new InboundDocumentLine
                {
                    InboundDocumentId = document.Id,
                    ItemId = item.Id,
                    ItemInstanceId = instance.Id,
                    SerialNumber = instance.SerialNumber,
                    Barcode = instance.Barcode,
                    Quantity = 1,
                    BinLocationId = bin!.Id,
                    Condition = string.IsNullOrEmpty(line.Condition) ? "Normal" : line.Condition,
                    Note = line.Note,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                });

                _db.CurrentItemLocations.Add(new CurrentItemLocation
                {
                    ItemInstanceId = instance.Id,
                    LocationType = LocationType.BinLocation,
                    WarehouseId = warehouse.Id,
                    BinLocationId = bin.Id,
                    ReferenceDocumentType = nameof(InboundDocument),
                    ReferenceDocumentId = document.Id,
                    ReferenceDocumentNo = document.DocumentNo,
                    UpdatedLocationAt = request.DocumentDate,
                    UpdatedLocationBy = user.UserName,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                });

                await ApplyStockDeltaAsync(warehouse.Id, bin.Id, item.Id, instance.Status, 1, user, cancellationToken);
                AddHistory(instance.Id, MovementActionType.Inbound, null, null, "Supplier", LocationType.BinLocation, bin.Id, bin.FullPath, ItemStatus.Reserved, instance.Status, nameof(InboundDocument), document.Id, document.DocumentNo, line.Note, user);
                AddInventoryTransaction(InventoryTransactionType.Inbound, item.Id, instance.Id, warehouse.Id, bin.Id, 1, instance.Status, nameof(InboundDocument), document.Id, document.DocumentNo, user);

                // Ghi log kiểm toán cho phếu nhập
                _db.InboundDocumentLogs.Add(new InboundDocumentLog
                {
                    InboundDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    Action = "InboundReceive",
                    OldStatus = "Reserved",
                    NewStatus = instance.Status.ToString(),
                    Receiver = receiverDisplay,
                    ReceiverPhone = request.ReceiverPhone,
                    ReceiverDepartment = request.ReceiverDepartment,
                    DepartmentOwner = request.ApprovedBy,
                    OldLocationText = "Supplier",
                    NewLocationText = bin.FullPath,
                    PerformedBy = user.UserName,
                    Timestamp = request.DocumentDate,
                    Note = line.Note
                });
            }

            AddPostSideEffects("Inbound", nameof(InboundDocument), document.Id, document.DocumentNo, user, "Inbound posted.");
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InboundDocument), document.Id, document.DocumentNo, now), "Inbound posted.");
        }

        foreach (var line in request.Lines)
        {
            var item = await FindItemByCodeAsync(line.ItemCode, cancellationToken);
            var bin = await FindBinByCodeAsync(line.BinCode, cancellationToken);
            var serialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim();

            var instance = new ItemInstance
            {
                ItemId = item!.Id,
                SerialNumber = serialNumber,
                Barcode = serialNumber, // Barcode = SerialNumber
                MT = line.MT,
                Status = ResolveInboundStatus(line.Condition),
                TrackingType = ItemTrackingType.LocationTracked,
                OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                CreatedAt = request.DocumentDate, CreatedBy = user.UserName
            };
            _db.ItemInstances.Add(instance);
            await _db.SaveChangesAsync(cancellationToken);

            _db.InboundDocumentLines.Add(new InboundDocumentLine
            {
                InboundDocumentId = oldDocument.Id, ItemId = item.Id, ItemInstanceId = instance.Id,
                SerialNumber = instance.SerialNumber, Barcode = instance.Barcode,
                Quantity = 1, BinLocationId = bin!.Id,
                Condition = string.IsNullOrEmpty(line.Condition)? "Normal": line.Condition, Note = line.Note,
                CreatedAt = request.DocumentDate, CreatedBy = user.UserName
            });

            _db.CurrentItemLocations.Add(new CurrentItemLocation
            {
                ItemInstanceId = instance.Id, LocationType = LocationType.BinLocation,
                WarehouseId = warehouse.Id, BinLocationId = bin.Id,
                ReferenceDocumentType = nameof(InboundDocument), ReferenceDocumentId = oldDocument.Id,
                ReferenceDocumentNo = oldDocument.DocumentNo,
                UpdatedLocationAt = request.DocumentDate, UpdatedLocationBy = user.UserName,
                CreatedAt = request.DocumentDate, CreatedBy = user.UserName
            });

            await ApplyStockDeltaAsync(warehouse.Id, bin.Id, item.Id, instance.Status, line.Quantity, user, cancellationToken);
            AddHistory(instance.Id, MovementActionType.Inbound, null, null, "Supplier", LocationType.BinLocation, bin.Id, bin.FullPath, ItemStatus.Reserved, instance.Status, nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, line.Note, user);
            AddInventoryTransaction(InventoryTransactionType.Inbound, item.Id, instance.Id, warehouse.Id, bin.Id, 1, instance.Status, nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, user);

            // Ghi log kiểm toán cho phếu nhập (bổ sung)
            _db.InboundDocumentLogs.Add(new InboundDocumentLog
            {
                InboundDocumentId = oldDocument.Id,
                ItemInstanceId = instance.Id,
                Action = "InboundReceive",
                OldStatus = "Reserved",
                NewStatus = instance.Status.ToString(),
                Receiver = receiverDisplay,
                ReceiverPhone = request.ReceiverPhone,
                ReceiverDepartment = request.ReceiverDepartment,
                DepartmentOwner = request.DepartmentOwner,
                OldLocationText = "Supplier",
                NewLocationText = bin.FullPath,
                PerformedBy = user.UserName,
                Timestamp = request.DocumentDate,
                Note = line.Note
            });
        }

        AddPostSideEffects("Inbound", nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, user, "Inbound posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, now), "Inbound posted.");
    }

    private async Task<List<string>> ValidateInboundAsync(InboundRequest request, Warehouse warehouse, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (!request.Lines.Any()) { errors.Add("At least one inbound line is required."); }
        //if(string.IsNullOrEmpty(request.DocumentNo)) errors.Add("Document no is required.");

        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var binCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in request.Lines)
        {
            if (line.Quantity != 1) errors.Add("This implementation tracks one item instance per line; quantity must be 1.");

            var item = await FindItemByCodeAsync(line.ItemCode, cancellationToken);
            if (item == null) { errors.Add($"Item {line.ItemCode} not found."); continue; }
            if (item.IsSerialManaged && string.IsNullOrWhiteSpace(line.SerialNumber)) errors.Add($"Item {item.ItemCode} requires serial number.");

            if (!string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                var sn = line.SerialNumber.Trim();
                if (!serials.Add(sn)) errors.Add($"Serial {sn} is duplicated in this inbound document.");
                if (await _db.ItemInstances.AnyAsync(x => x.Item != null && x.Item.ItemCode == item.ItemCode && x.SerialNumber == sn, cancellationToken))
                    errors.Add($"Serial {sn} already exists for item {item.ItemCode}.");
            }

            var bin = await FindBinByCodeAsync(line.BinCode, cancellationToken);
            if (bin == null) { errors.Add($"BinCode {line.BinCode} not found."); continue; }
            if (bin.WarehouseId != warehouse.Id) { errors.Add($"BinCode {line.BinCode} does not belong to warehouse {warehouse.WarehouseCode}."); continue; }
            if (!binCodes.Add(line.BinCode)) errors.Add($"Bin {line.BinCode} is already used in another inbound line.");
            if (await BinHasActiveItemAsync(bin.Id, null, cancellationToken)) errors.Add($"Bin {bin.FullPath} already contains another active item.");
        }
        return errors;
    }

    /// <summary>
    /// Maps the inbound condition string to the correct ItemStatus.
    /// "Normal" → Normal (0), "Damaged" → Damaged (6), "Scrapped" → Scrapped (11).
    /// InStock (legacy) also accepted and mapped to Normal.
    /// </summary>
    private static ItemStatus ResolveInboundStatus(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return ItemStatus.Normal;
        if (Enum.TryParse<ItemStatus>(condition.Trim(), true, out var parsed))
        {
            // Legacy InStock maps to Normal
            return parsed == ItemStatus.InStock ? ItemStatus.Normal : parsed;
        }
        return ItemStatus.Normal;
    }
}
