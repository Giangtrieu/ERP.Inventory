using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
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

        var postingContext = await PreloadInboundPostingContextAsync(request, warehouse, cancellationToken);
        var errors = ValidateInbound(request, warehouse, postingContext);
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        await using var transaction = await BeginOperationTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var lifecycleBatchId = Guid.NewGuid();

        ExternalParty? sourceParty = null;
        if (!string.IsNullOrWhiteSpace(request.SourcePartyer))
        {
            var parts = request.SourcePartyer.Split('-', 2);
            request.SourcePartyCode = parts[0].Trim();
            request.SourcePartyName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(request.SourcePartyCode))
            {
                sourceParty = await GetOrCreatePartyByCodeTrackedAsync(request.SourcePartyCode, request.SourcePartyName, ExternalPartyType.Supplier, request.ReceiverPhone, user.UserName, cancellationToken);
            }
        }

        int? receiverId = null;
        ExternalParty? receiverParty = null;
        string receiverDisplay = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.ReceiverCode) && !string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            receiverParty = await GetOrCreatePartyByCodeTrackedAsync(request.ReceiverCode, request.ReceiverName, ExternalPartyType.Receiver, request.ReceiverPhone, user.UserName, cancellationToken);
            receiverId = receiverParty.Id == 0 ? null : receiverParty.Id;
            receiverDisplay = $"{receiverParty.PartyCode}-{receiverParty.Name}";
        }

        if (!string.IsNullOrWhiteSpace(request.DepartmentOwner))
        {
            await GetOrCreatePartyByNameAsync(request.DepartmentOwner, "", ExternalPartyType.DepartmentOwner, "OWNER", "", user.UserName, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovedBy))
        {
            await GetOrCreatePartyByNameAsync(request.DepartmentOwner, "", ExternalPartyType.Approver, "APP", "", user.UserName, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiverDepartment))
        {
            await GetOrCreatePartyByNameAsync(request.ReceiverDepartment, "", ExternalPartyType.Department, "DEP", "", user.UserName, now, cancellationToken);
        }

        if ((!string.IsNullOrEmpty(request.OwnerName) && request.OwnerName.ToUpper() == "TE") || request.DocumentNo == "0000000001") request.DocumentNo = "0000000001";

        var oldDocument = await FindInboundDocumentByCodeAsync(request.DocumentNo.Trim(), cancellationToken);

        if (oldDocument == null)
        {
            var document = new InboundDocument
            {
                DocumentNo = request.DocumentNo.Trim().ToLower() != "auto" ? request.DocumentNo.Trim() : _documentNumbers.Next("", DateTime.UtcNow),
                DocumentDate = request.DocumentDate,
                SourceExternalParty = sourceParty,
                ReceiverId = receiverId,
                Receiver = receiverParty,
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
                AddInboundLineEffects(document, line, warehouse, request, user, receiverDisplay, request.ApprovedBy, lifecycleBatchId, postingContext, stockQuantityDelta: 1, setInstanceDocumentNo: true);
            }

            AddPostSideEffects("Inbound", nameof(InboundDocument), document.Id, document.DocumentNo, user, "Inbound posted.");
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InboundDocument), document.Id, document.DocumentNo, now), "Inbound posted.");
        }

        foreach (var line in request.Lines)
        {
            AddInboundLineEffects(oldDocument, line, warehouse, request, user, receiverDisplay, request.DepartmentOwner, lifecycleBatchId, postingContext, stockQuantityDelta: line.Quantity, setInstanceDocumentNo: true);
        }

        AddPostSideEffects("Inbound", nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, user, "Inbound posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto(nameof(InboundDocument), oldDocument.Id, oldDocument.DocumentNo, now), "Inbound posted.");
    }

    private async Task<InboundPostingContext> PreloadInboundPostingContextAsync(InboundRequest request, Warehouse warehouse, CancellationToken cancellationToken)
    {
        var itemCodes = request.Lines
            .Select(x => NormalizeLookupCode(x.ItemCode))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var binCodes = request.Lines
            .Select(x => NormalizeLookupCode(x.BinCode))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serials = request.Lines
            .Select(x => NormalizeSerial(x.SerialNumber))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var statuses = request.Lines
            .Select(x => ResolveInboundStatus(x.Condition))
            .Distinct()
            .ToArray();

        var items = itemCodes.Length == 0
            ? new List<Item>()
            : await _db.Items
                .AsNoTracking()
                .Where(x => itemCodes.Contains(x.ItemCode) && x.IsActive)
                .ToListAsync(cancellationToken);
        var bins = binCodes.Length == 0
            ? new List<BinLocation>()
            : await _db.BinLocations
                .AsNoTracking()
                .Where(x => binCodes.Contains(x.BinCode) && x.IsActive)
                .ToListAsync(cancellationToken);

        var itemIds = items.Select(x => x.Id).Distinct().ToArray();
        var binIds = bins.Select(x => x.Id).Distinct().ToArray();
        var existingSerialRows = itemIds.Length == 0 || serials.Length == 0
            ? new List<ItemInstance>()
            : await _db.ItemInstances
                .AsNoTracking()
                .Include(x => x.Item)
                .Where(x => itemIds.Contains(x.ItemId) && x.SerialNumber != null && serials.Contains(x.SerialNumber))
                .ToListAsync(cancellationToken);
        var occupiedBinIds = binIds.Length == 0
            ? new List<int>()
            : await _db.CurrentItemLocations
                .AsNoTracking()
                .Where(x =>
                    x.BinLocationId.HasValue &&
                    binIds.Contains(x.BinLocationId.Value) &&
                    x.ItemInstance != null &&
                    x.ItemInstance.IsActive &&
                    x.ItemInstance.Status != ItemStatus.Lost &&
                    x.ItemInstance.Status != ItemStatus.Disposed)
                .Select(x => x.BinLocationId!.Value)
                .ToListAsync(cancellationToken);
        var stockBalances = itemIds.Length == 0 || binIds.Length == 0 || statuses.Length == 0
            ? new List<StockBalance>()
            : await _db.StockBalances
                .Where(x =>
                    x.WarehouseId == warehouse.Id &&
                    x.BinLocationId.HasValue &&
                    binIds.Contains(x.BinLocationId.Value) &&
                    itemIds.Contains(x.ItemId) &&
                    statuses.Contains(x.Status))
                .ToListAsync(cancellationToken);

        var context = new InboundPostingContext();
        foreach (var item in items)
        {
            context.Items[NormalizeLookupCode(item.ItemCode)] = item;
        }
        foreach (var bin in bins)
        {
            context.Bins[NormalizeLookupCode(bin.BinCode)] = bin;
        }
        foreach (var instance in existingSerialRows)
        {
            if (instance.Item == null || string.IsNullOrWhiteSpace(instance.SerialNumber)) continue;
            context.ExistingSerialKeys.Add(InboundSerialKey(instance.Item.ItemCode, instance.SerialNumber));
        }
        foreach (var binId in occupiedBinIds)
        {
            context.OccupiedBinIds.Add(binId);
        }
        foreach (var balance in stockBalances.Concat(_db.StockBalances.Local))
        {
            context.StockBalances[StockBalanceKey(balance.WarehouseId, balance.BinLocationId, balance.ItemId, balance.Status)] = balance;
        }

        return context;
    }

    private static List<string> ValidateInbound(InboundRequest request, Warehouse warehouse, InboundPostingContext context)
    {
        var errors = new List<string>();
        if (!request.Lines.Any()) { errors.Add("At least one inbound line is required."); }

        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var binCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in request.Lines)
        {
            if (line.Quantity != 1) errors.Add("This implementation tracks one item instance per line; quantity must be 1.");

            var item = ResolveInboundItem(line, context);
            if (item == null) { errors.Add($"Item {line.ItemCode} not found."); continue; }
            if (item.IsSerialManaged && string.IsNullOrWhiteSpace(line.SerialNumber)) errors.Add($"Item {item.ItemCode} requires serial number.");

            if (!string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                var sn = line.SerialNumber.Trim();
                if (!serials.Add(sn)) errors.Add($"Serial {sn} is duplicated in this inbound document.");
                if (context.ExistingSerialKeys.Contains(InboundSerialKey(item.ItemCode, sn)))
                    errors.Add($"Serial {sn} already exists for item {item.ItemCode}.");
            }

            var bin = ResolveInboundBin(line, context);
            if (bin == null) { errors.Add($"BinCode {line.BinCode} not found."); continue; }
            if (bin.WarehouseId != warehouse.Id) { errors.Add($"BinCode {line.BinCode} does not belong to warehouse {warehouse.WarehouseCode}."); continue; }
            if (!binCodes.Add(line.BinCode)) errors.Add($"Bin {line.BinCode} is already used in another inbound line.");
            if (context.OccupiedBinIds.Contains(bin.Id)) errors.Add($"Bin {bin.FullPath} already contains another active item.");
        }
        return errors;
    }

    private async Task<ExternalParty> GetOrCreatePartyByCodeTrackedAsync(string code, string name, ExternalPartyType type, string phone, string userName, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        var party = await _db.ExternalParties.FirstOrDefaultAsync(x => x.PartyCode == normalizedCode && x.PartyType == type && x.IsActive, cancellationToken);
        if (party != null) return party;

        party = new ExternalParty
        {
            PartyCode = normalizedCode,
            Name = name,
            PartyType = type,
            Phone = phone,
            CreatedBy = userName
        };
        _db.ExternalParties.Add(party);
        return party;
    }

    private void AddInboundLineEffects(InboundDocument document, InboundLineRequest line, Warehouse warehouse, InboundRequest request, CurrentUserContext user, 
        string receiverDisplay, string? logDepartmentOwner,Guid lifecycleBatchId,  InboundPostingContext context, decimal stockQuantityDelta,  bool setInstanceDocumentNo)
    {
        var item = ResolveInboundItem(line, context)!;
        var bin = ResolveInboundBin(line, context)!;
        var serialNumber = string.IsNullOrWhiteSpace(line.SerialNumber) ? null : line.SerialNumber.Trim();
        var status = ResolveInboundStatus(line.Condition);

        var instance = new ItemInstance
        {
            ItemId = item.Id,
            SerialNumber = serialNumber,
            Barcode = serialNumber,
            MT = line.MT,
            DocumentNo = setInstanceDocumentNo ? document.DocumentNo : null,
            Status = status,
            TrackingType = ItemTrackingType.LocationTracked,
            OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
            CreatedAt = request.DocumentDate,
            CreatedBy = user.UserName
        };
        _db.ItemInstances.Add(instance);

        _db.InboundDocumentLines.Add(new InboundDocumentLine
        {
            InboundDocumentId = document.Id,
            ItemId = item.Id,
            ItemInstance = instance,
            SerialNumber = instance.SerialNumber,
            Barcode = instance.Barcode,
            Quantity = 1,
            BinLocationId = bin.Id,
            Condition = string.IsNullOrEmpty(line.Condition) ? "Normal" : line.Condition,
            Note = line.Note,
            CreatedAt = request.DocumentDate,
            CreatedBy = user.UserName
        });

        _db.CurrentItemLocations.Add(new CurrentItemLocation
        {
            ItemInstance = instance,
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

        ApplyStockDelta(context, warehouse.Id, bin.Id, item.Id, status, stockQuantityDelta, user);
        AddHistory(instance, MovementActionType.Inbound, null, null, "Supplier", LocationType.BinLocation, bin.Id, bin.FullPath, ItemStatus.Reserved, status, nameof(InboundDocument), document.Id, document.DocumentNo, line.Note, user, lifecycleBatchId);
        AddInventoryTransaction(InventoryTransactionType.Inbound, item.Id, instance, warehouse.Id, bin.Id, 1, status, nameof(InboundDocument), document.Id, document.DocumentNo, user, lifecycleBatchId);

        _db.InboundDocumentLogs.Add(new InboundDocumentLog
        {
            InboundDocumentId = document.Id,
            ItemInstance = instance,
            Action = "InboundReceive",
            LifecycleBatchId = lifecycleBatchId,
            OldStatus = "Reserved",
            NewStatus = status.ToString(),
            Receiver = receiverDisplay,
            ReceiverPhone = request.ReceiverPhone,
            ReceiverDepartment = request.ReceiverDepartment,
            DepartmentOwner = logDepartmentOwner,
            OldLocationText = "Supplier",
            NewLocationText = bin.FullPath,
            PerformedBy = user.UserName,
            Timestamp = request.DocumentDate,
            Note = line.Note
        });
    }

    private void ApplyStockDelta(InboundPostingContext context, int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user)
    {
        if (delta == 0) return;
        var key = StockBalanceKey(warehouseId, binLocationId, itemId, status);
        if (!context.StockBalances.TryGetValue(key, out var balance))
        {
            balance = new StockBalance
            {
                WarehouseId = warehouseId,
                BinLocationId = binLocationId,
                ItemId = itemId,
                Status = status,
                Quantity = 0,
                CreatedAt = _clock.UtcNow,
                CreatedBy = user.UserName
            };
            _db.StockBalances.Add(balance);
            context.StockBalances[key] = balance;
        }

        balance.Quantity += delta;
        balance.UpdatedAt = _clock.UtcNow;
        balance.UpdatedBy = user.UserName;
    }

    private void AddHistory(ItemInstance instance, MovementActionType action, LocationType? fromType, int? fromId, string? fromDisplay, LocationType? toType, int? toId, string? toDisplay, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, string? note, CurrentUserContext user, Guid? lifecycleBatchId = null)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstance = instance,
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
            LifecycleBatchId = lifecycleBatchId,
            Note = note,
            PerformedAt = _clock.UtcNow,
            PerformedBy = user.UserName
        });
    }

    private void AddInventoryTransaction(InventoryTransactionType type, int itemId, ItemInstance instance, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user, Guid? lifecycleBatchId = null)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionType = type,
            ItemId = itemId,
            ItemInstance = instance,
            WarehouseId = warehouseId,
            BinLocationId = binLocationId,
            QuantityDelta = quantityDelta,
            StatusAfter = statusAfter,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNo = documentNo,
            LifecycleBatchId = lifecycleBatchId,
            PostedAt = _clock.UtcNow,
            PostedBy = user.UserName
        });
    }

    private static Item? ResolveInboundItem(InboundLineRequest line, InboundPostingContext context)
        => context.Items.GetValueOrDefault(NormalizeLookupCode(line.ItemCode));

    private static BinLocation? ResolveInboundBin(InboundLineRequest line, InboundPostingContext context)
        => context.Bins.GetValueOrDefault(NormalizeLookupCode(line.BinCode));

    private static string NormalizeLookupCode(string? value)
        => (value ?? string.Empty).Trim();

    private static string NormalizeSerial(string? value)
        => (value ?? string.Empty).Trim();

    private static string InboundSerialKey(string itemCode, string serialNumber)
        => $"{NormalizeLookupCode(itemCode)}:{NormalizeSerial(serialNumber)}";

    private static string StockBalanceKey(int warehouseId, int? binLocationId, int itemId, ItemStatus status)
        => $"{warehouseId}:{binLocationId?.ToString() ?? string.Empty}:{itemId}:{(int)status}";

    private static ItemStatus ResolveInboundStatus(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return ItemStatus.Normal;
        if (Enum.TryParse<ItemStatus>(condition.Trim(), true, out var parsed))
        {
            return parsed == ItemStatus.InStock ? ItemStatus.Normal : parsed;
        }
        return ItemStatus.Normal;
    }

    private sealed class InboundPostingContext
    {
        public Dictionary<string, Item> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BinLocation> Bins { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingSerialKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<int> OccupiedBinIds { get; } = new();
        public Dictionary<string, StockBalance> StockBalances { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
