using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.GeometriesGraph;
using System.Collections.Generic;

namespace ERP.Inventory.Infrastructure.Services;


public sealed class QuantityInventoryService : InventoryOperationBase, IQuantityInventoryService
{
    public QuantityInventoryService(InventoryDbContext db, IDocumentNumberService documentNumbers, IDateTimeProvider clock)
        : base(db, documentNumbers, clock)
    {
    }

    public Task<ServiceResult<PostedDocumentDto>> ReceiveAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false)
        => PostAsync(request, QuantityInventoryDocumentType.Receive, user, cancellationToken, isEdit);

    public Task<ServiceResult<PostedDocumentDto>> IssueAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false)
        => PostAsync(request, QuantityInventoryDocumentType.Issue, user, cancellationToken, isEdit);

    public Task<ServiceResult<PostedDocumentDto>> AdjustAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false)
        => PostAsync(request, QuantityInventoryDocumentType.Adjust, user, cancellationToken, isEdit);

    public async Task<PagedResult<QuantityStockBalanceDto>> GetBalancesAsync(string? keyword, int? warehouseId, int? itemId, string? status, string? ownerName, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? 1000 : Math.Clamp(pageSize, 1, 500);

        var query = _db.QuantityStockBalances.AsNoTracking() .Include(x => x.Warehouse).Include(x => x.Item) .Where(x => x.Quantity != 0);

        query = ApplyReadScope(query, warehouseId, user);

        if (itemId.HasValue) query = query.Where(x => x.ItemId == itemId.Value);
        if (Enum.TryParse<ItemStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);
        if (!string.IsNullOrWhiteSpace(ownerName))
        {
            var ownerFilter = ownerName.Trim();
            query = query.Where(x => _db.ItemInstances.Any(i => i.ItemId == x.ItemId &&
                i.SerialNumber == x.SnCode &&i.TrackingType == ItemTrackingType.QuantityOnly &&
                i.OwnerName != null &&  i.OwnerName.Contains(ownerFilter)));
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            query = query.Where(x => x.SnCode.Contains(key) || x.Item!.ItemCode.Contains(key) || x.Item.DefaultName.Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);
        var groupedRows = await query.GroupBy(x => new
             {
                 x.ItemId, ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                 ItemName = x.Item != null ? x.Item.DefaultName : string.Empty, x.WarehouseId,
                 WarehouseCode = x.Warehouse != null? x.Warehouse.WarehouseCode : string.Empty
             }).Select(g => new
             {
                 g.Key.ItemId, g.Key.ItemCode,
                 g.Key.ItemName, g.Key.WarehouseId,
                 g.Key.WarehouseCode, Quantity = g.Sum(x => x.Quantity),
                 LastUpdatedAt = g.Max(x => x.UpdatedAt ?? x.CreatedAt), Status = g.Select(x => x.Status).FirstOrDefault()
             }).OrderBy(x => x.WarehouseCode).ThenBy(x => x.ItemCode)
             .Skip((page - 1) * pageSize).Take(pageSize) .ToListAsync(cancellationToken);
                var itemIds = groupedRows.Select(x => x.ItemId).Distinct().ToList();
                var ownerDict = await _db.ItemInstances .AsNoTracking() .Where(x => itemIds.Contains(x.ItemId) &&x.TrackingType == ItemTrackingType.QuantityOnly) .GroupBy(x => x.ItemId)
                    .Select(g => new
                    {
                        ItemId = g.Key,OwnerName = g.Select(x => x.OwnerName).FirstOrDefault()
                    }) .ToDictionaryAsync(x => x.ItemId, x => x.OwnerName, cancellationToken);
        var rows = groupedRows .Select(x => new QuantityStockBalanceDto
            {
                ItemId = x.ItemId, ItemCode = x.ItemCode,
                ItemName = x.ItemName, WarehouseId = x.WarehouseId,
                WarehouseCode = x.WarehouseCode, Quantity = x.Quantity,
                Status = x.Status.ToString(), LastUpdatedAt = x.LastUpdatedAt,
                OwnerName = ownerDict.TryGetValue(x.ItemId, out var owner) ? owner : null, 
            }).ToArray();
        return new PagedResult<QuantityStockBalanceDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<IReadOnlyCollection<QuantityInventoryTransactionDto>> GetTransactionsAsync(string? keyword, int? warehouseId, int? itemId, int take, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var query = _db.QuantityInventoryTransactions.AsNoTracking().Include(x => x.Item) .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value) ? query.Where(x => x.WarehouseId == warehouseId.Value)  : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        if (itemId.HasValue) query = query.Where(x => x.ItemId == itemId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim(); query = query.Where(x =>  x.SnCode.Contains(key) ||
                x.DocumentNo.Contains(key) || x.Item!.ItemCode.Contains(key));
        }

        return await query .OrderByDescending(x => x.PostedAt).Take(take)
            .Select(x => new QuantityInventoryTransactionDto
            {
                Id = x.Id, TransactionType = x.TransactionType.ToString(),
                DocumentNo = x.DocumentNo,  PostedAt = x.PostedAt,
                ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                SnCode = x.SnCode,   Status = x.StatusAfter.ToString(),
                QuantityDelta = x.QuantityDelta,  PostedBy = x.PostedBy
            }) .ToArrayAsync(cancellationToken);
    }

    // ─── Core Post Logic ─────────────────────────────────────────────
    private async Task<ServiceResult<PostedDocumentDto>> PostAsync(QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user, CancellationToken cancellationToken, bool isEdit = false)
    {
        if(isEdit == false) return await PostNewAsync(request, type, user, cancellationToken);

        var errors = ValidateHeader(request, user);
        var lines = request.Lines.Where(x => !IsEmptyLine(x)).ToArray();

        if (lines.Length == 0) errors.Add("At least one line is required.");
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var now = _clock.UtcNow;
        var documentNo = string.IsNullOrWhiteSpace(request.DocumentNo)? _documentNumbers.Next(type == QuantityInventoryDocumentType.Receive ? "QIN" :
            type == QuantityInventoryDocumentType.Issue ? "QOUT" : "QADJ", request.DocumentDate): request.DocumentNo.Trim();
        await using var tx = await BeginOperationTransactionAsync(cancellationToken);

        var item = await ResolveOrCreateItemAsync(request, user, cancellationToken);
        if (item == null)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail("ItemCode is required.");
        }

        // ── LẤY DOCUMENT ─────────────────────────────────────────────
        var document = await _db.QuantityInventoryDocuments.FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);
        var isNewDocument = document == null;

        if (isNewDocument)
        {
            document = new QuantityInventoryDocument
            {
                DocumentNo = documentNo,
                DocumentDate = request.DocumentDate,
                PostedAt = request.DocumentDate,
                DocumentType = type,
                WarehouseId = request.WarehouseId,
                ApprovedBy = request.ApprovedBy,
                Note = request.Note,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.QuantityInventoryDocuments.Add(document);
        }
        else
        {
            _db.Entry(document).State = EntityState.Modified;
            document.DocumentDate = request.DocumentDate;
            document.PostedAt = request.DocumentDate;
            document.ApprovedBy = request.ApprovedBy;
            document.Note = request.Note;
            document.UpdatedAt = now;
            document.UpdatedBy = user.UserName;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── XỬ LÝ LINES ───────────────────────────────────────────────
        var newSnCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var oldLines = isNewDocument ? new List<QuantityInventoryDocumentLine>(): await _db.QuantityInventoryDocumentLines
                .Where(x => x.QuantityInventoryDocumentId == document.Id).ToListAsync(cancellationToken);

        foreach (var line in lines)
        {
            var snCode = NormalizeSn(line.SnCode);
            if (string.IsNullOrWhiteSpace(snCode))
            {
                errors.Add("SN is required.");
                continue;
            }

            newSnCodes.Add(snCode);
            var error = await ProcessLineWithUpdateAsync(document, line, item, type, request, user, now, isNewDocument, isEdit, cancellationToken);
            if (error != null) errors.Add(error);
        }

        // Xóa lines cũ không còn tồn tại (chỉ khi Edit)
        if (!isNewDocument)
        {
            foreach (var oldLine in oldLines)
            {
                var normalizedOldSn = NormalizeSn(oldLine.SnCode);
                if (newSnCodes.Contains(normalizedOldSn)) continue;

                var error = await ReverseDeletedLineAsync(oldLine, document, request.WarehouseId, type, user, now, cancellationToken);
                if (error != null) errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail(errors);
        }

        await _db.SaveChangesAsync(cancellationToken);
        // Backfill DocumentId
        foreach (var txRecord in _db.QuantityInventoryTransactions.Local.Where(x => x.DocumentNo == documentNo && x.DocumentId == 0))
        {
            txRecord.DocumentId = document.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        AddPostSideEffects(type.ToString(), nameof(QuantityInventoryDocument), document.Id, documentNo, user, "Quantity inventory posted.");
        await tx.CommitAsync(cancellationToken);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto("QuantityInventory", document.Id, documentNo, now), "Quantity inventory posted.");


    }

    private async Task<ServiceResult<PostedDocumentDto>> PostNewAsync(QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var errors = ValidateHeader(request, user);
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var lines = request.Lines.Where(x => !IsEmptyLine(x)).ToArray();
        if (lines.Length == 0) errors.Add("At least one line is required.");
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var now = _clock.UtcNow;
        var documentNo = string.IsNullOrWhiteSpace(request.DocumentNo) ? _documentNumbers.Next(type == QuantityInventoryDocumentType.Receive ?
            "QIN" : type == QuantityInventoryDocumentType.Issue ? "QOUT" : "QADJ", request.DocumentDate) : request.DocumentNo.Trim();

        // ── Begin transaction ─────────────────────────────────────────
        await using var tx = await BeginOperationTransactionAsync(cancellationToken);

        // ── Check duplicate DocumentNo ────────────────────────────────
        var document = await _db.QuantityInventoryDocuments.FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);

        // ── Resolve/Create ItemCategory (once, outside line loop) ─────
        var normalizedCategoryCode = (request.ItemCategoryCode ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedItemCode = (request.ItemCode ?? string.Empty).Trim().ToUpperInvariant();

        ItemCategory? itemCategory = null;
        if (!string.IsNullOrWhiteSpace(normalizedCategoryCode))
        {
            itemCategory = await _db.ItemCategories
                .FirstOrDefaultAsync(x => x.CategoryCode == normalizedCategoryCode && x.IsActive, cancellationToken);

            if (itemCategory == null)
            {
                itemCategory = new ItemCategory
                {
                    CategoryCode = normalizedCategoryCode,
                    Name = normalizedCategoryCode,
                    IsActive = true,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.ItemCategories.Add(itemCategory);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // ── Resolve/Create Item (once, outside line loop) ─────────────
        Item? item = null;
        if (!string.IsNullOrWhiteSpace(normalizedItemCode))
        {
            item = await _db.Items.FirstOrDefaultAsync(x => x.ItemCode == normalizedItemCode && x.IsActive, cancellationToken);
            if (item == null)
            {
                var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == "PCS", cancellationToken);

                item = new Item
                {
                    ItemCode = normalizedItemCode,
                    DefaultName = normalizedItemCode,
                    CategoryId = itemCategory?.Id ?? 0,
                    UnitId = unit?.Id ?? 0,
                    IsSerialManaged = false, // QuantityOnly items không cần serial tracking
                    IsActive = true,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.Items.Add(item);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        if (item == null)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail("ItemCode is required.");
        }
        // ── Create document header ────────────────────────────────────
        if (document == null)
        {
            document = new QuantityInventoryDocument
            {
                DocumentNo = documentNo,
                DocumentDate = request.DocumentDate,
                PostedAt = request.DocumentDate,
                DocumentType = type,
                WarehouseId = request.WarehouseId,
                ApprovedBy = request.ApprovedBy,
                Note = request.Note,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.QuantityInventoryDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
        }
        // ── Process lines ─────────────────────────────────────────────
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                errors.Add("Quantity must be greater than zero.");
                continue;
            }

            var snCode = NormalizeSn(line.SnCode);
            if (string.IsNullOrWhiteSpace(snCode))
            {
                errors.Add("SN is required.");
                continue;
            }

            // ── Resolve/Create ItemInstance (QuantityOnly) ────────────
            var instance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.ItemId == item.Id && x.SerialNumber == snCode && x.TrackingType == ItemTrackingType.QuantityOnly, cancellationToken);

            if (instance != null && type == QuantityInventoryDocumentType.Receive)
            {
                errors.Add($"Serial {snCode} already exists.");
                continue;
            }
            if (instance == null)
            {
                instance = new ItemInstance
                {
                    ItemId = item.Id,
                    SerialNumber = snCode,
                    Barcode = snCode,
                    Status = ResolveInboundStatus(line.Status),
                    DocumentNo = documentNo,
                    TrackingType = ItemTrackingType.QuantityOnly,
                    OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                    IsActive = true,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.ItemInstances.Add(instance);
                await _db.SaveChangesAsync(cancellationToken);
            }
            // ── Upsert QuantityStockBalance ───────────────────────────
            var delta = type switch
            {
                QuantityInventoryDocumentType.Issue => -line.Quantity,
                QuantityInventoryDocumentType.Adjust => 0,
                _ => line.Quantity
            };

            if (type != QuantityInventoryDocumentType.Receive) { instance.Status = ResolveInboundStatus(line.Status); }
            var balance = await _db.QuantityStockBalances.FirstOrDefaultAsync(x => x.WarehouseId == request.WarehouseId &&
                x.ItemId == item.Id && x.SnCode == snCode &&
                x.Status == ResolveInboundStatus(line.Status), cancellationToken);

            if (type == QuantityInventoryDocumentType.Issue && (balance == null || balance.Quantity < line.Quantity))
            {
                errors.Add($"Insufficient quantity for item {item.ItemCode} SN {snCode}.");
                continue;
            }

            if (balance == null)
            {
                balance = new QuantityStockBalance
                {
                    WarehouseId = request.WarehouseId,
                    ItemId = item.Id,
                    SnCode = snCode,
                    Status = ResolveInboundStatus(line.Status),
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.QuantityStockBalances.Add(balance);
            }

            if (type != QuantityInventoryDocumentType.Adjust) balance.Quantity += delta;
            balance.UpdatedAt = now;
            balance.UpdatedBy = user.UserName;

            document.Lines.Add(new QuantityInventoryDocumentLine
            {
                ItemId = item.Id,
                SnCode = snCode,
                Status = ResolveInboundStatus(line.Status),
                Quantity = line.Quantity,
                Note = line.Note,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            });

            _db.QuantityInventoryTransactions.Add(new QuantityInventoryTransaction
            {
                TransactionType = type,
                WarehouseId = request.WarehouseId,
                ItemId = item.Id,
                SnCode = snCode,
                StatusAfter = ResolveInboundStatus(line.Status),
                QuantityDelta = delta,
                DocumentNo = documentNo,
                PostedAt = request.DocumentDate,
                PostedBy = user.UserName
            });
        }

        if (errors.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken); return ServiceResult<PostedDocumentDto>.Fail(errors);
        }
        await _db.SaveChangesAsync(cancellationToken);

        // Backfill DocumentId on transactions
        foreach (var txRecord in _db.QuantityInventoryTransactions.Local.Where(x => x.DocumentNo == documentNo))
        {
            txRecord.DocumentId = document.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        AddPostSideEffects(type.ToString(), nameof(QuantityInventoryDocument), document.Id, documentNo, user, "Quantity inventory posted.");
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto("QuantityInventory", document.Id, documentNo, now), "Quantity inventory posted.");
    }

    private async Task<string?> ProcessLineWithUpdateAsync(QuantityInventoryDocument document,QuantityInventoryLineRequest line,
    Item item, QuantityInventoryDocumentType type, QuantityInventoryRequest request, CurrentUserContext user, DateTime now,
    bool isNewDocument, bool isEdit, CancellationToken ct)
    {
        if (line.Quantity <= 0)
            return "Quantity must be greater than zero.";

        var snCode = NormalizeSn(line.SnCode);

        // Kiểm tra SN tồn tại khi tạo mới
        var existingInstance = await _db.ItemInstances
            .FirstOrDefaultAsync(x => x.ItemId == item.Id &&
                                      x.SerialNumber == snCode &&
                                      x.TrackingType == ItemTrackingType.QuantityOnly, ct);

        if (isNewDocument && !isEdit)
        {
            if (existingInstance != null && type == QuantityInventoryDocumentType.Receive)
                return $"Serial {snCode} already exists.";
        }

        // ── ItemInstance (Create or Update) ─────────────────────────────
        if (existingInstance == null)
        {
            existingInstance = new ItemInstance
            {
                ItemId = item.Id,
                SerialNumber = snCode,
                Barcode = snCode,
                Status = ResolveInboundStatus(line.Status),
                DocumentNo = document.DocumentNo,
                TrackingType = ItemTrackingType.QuantityOnly,
                OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                IsActive = true,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.ItemInstances.Add(existingInstance);
        }
        else
        {
            existingInstance.Status = ResolveInboundStatus(line.Status);
            existingInstance.DocumentNo = document.DocumentNo;
            existingInstance.OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim();
        }

        // ── Stock Balance & Delta ───────────────────────────────────────
        var delta = type switch
        {
            QuantityInventoryDocumentType.Issue => -line.Quantity,
            QuantityInventoryDocumentType.Adjust => 0,
            _ => line.Quantity
        };

        var balance = await _db.QuantityStockBalances
            .FirstOrDefaultAsync(x => x.WarehouseId == request.WarehouseId &&
                                      x.ItemId == item.Id &&
                                      x.SnCode == snCode &&
                                      x.Status == ResolveInboundStatus(line.Status), ct);

        if (balance == null)
        {
            balance = new QuantityStockBalance
            {
                WarehouseId = request.WarehouseId,
                ItemId = item.Id,
                SnCode = snCode,
                Status = ResolveInboundStatus(line.Status),
                Quantity = 0,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.QuantityStockBalances.Add(balance);
        }

        if (type == QuantityInventoryDocumentType.Issue && balance.Quantity < line.Quantity)
            return $"Insufficient quantity for item {item.ItemCode} SN {snCode}.";

        if (type != QuantityInventoryDocumentType.Adjust)
            balance.Quantity += delta;

        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;

        // ── Document Line (Luôn tạo mới cho document này) ───────────────
        document.Lines.Add(new QuantityInventoryDocumentLine
        {
            ItemId = item.Id,
            SnCode = snCode,
            Status = ResolveInboundStatus(line.Status),
            Quantity = line.Quantity,
            Note = line.Note,
            CreatedAt = request.DocumentDate,
            CreatedBy = user.UserName
        });

        // ── Transaction ─────────────────────────────────────────────────
        _db.QuantityInventoryTransactions.Add(new QuantityInventoryTransaction
        {
            TransactionType = type,
            WarehouseId = request.WarehouseId,
            ItemId = item.Id,
            SnCode = snCode,
            StatusAfter = ResolveInboundStatus(line.Status),
            QuantityDelta = delta,
            DocumentNo = document.DocumentNo,
            PostedAt = request.DocumentDate,
            PostedBy = user.UserName
        });

        return null;
    }
    private async Task<Item?> ResolveOrCreateItemAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken ct)
    {
        var normalizedCategoryCode = (request.ItemCategoryCode ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedItemCode = (request.ItemCode ?? string.Empty).Trim().ToUpperInvariant();

        ItemCategory? itemCategory = null;
        if (!string.IsNullOrWhiteSpace(normalizedCategoryCode))
        {
            itemCategory = await _db.ItemCategories
                .FirstOrDefaultAsync(x => x.CategoryCode == normalizedCategoryCode && x.IsActive, ct);

            if (itemCategory == null)
            {
                itemCategory = new ItemCategory
                {
                    CategoryCode = normalizedCategoryCode,
                    Name = normalizedCategoryCode,
                    IsActive = true,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.ItemCategories.Add(itemCategory);
                await _db.SaveChangesAsync(ct);
            }
        }

        Item? item = null;
        if (!string.IsNullOrWhiteSpace(normalizedItemCode))
        {
            item = await _db.Items.FirstOrDefaultAsync(x => x.ItemCode == normalizedItemCode && x.IsActive, ct);
            if (item == null)
            {
                var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == "PCS", ct);

                item = new Item
                {
                    ItemCode = normalizedItemCode,
                    DefaultName = normalizedItemCode,
                    CategoryId = itemCategory?.Id ?? 0,
                    UnitId = unit?.Id ?? 0,
                    IsSerialManaged = false,
                    IsActive = true,
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                };
                _db.Items.Add(item);
                await _db.SaveChangesAsync(ct);
            }
        }
        return item;
    }

    private async Task<string?> ProcessSingleLineAsync(QuantityInventoryDocument document, QuantityInventoryLineRequest line,
        Item item, QuantityInventoryDocumentType type, QuantityInventoryRequest request, CurrentUserContext user,
        DateTime now, bool isEdit,  CancellationToken ct)
    {
        if (line.Quantity <= 0)
            return "Quantity must be greater than zero.";

        var snCode = NormalizeSn(line.SnCode);
        if (string.IsNullOrWhiteSpace(snCode))
            return "SN is required.";

        // ItemInstance
        var instance = await _db.ItemInstances.FirstOrDefaultAsync(x =>
            x.ItemId == item.Id && x.SerialNumber == snCode && x.TrackingType == ItemTrackingType.QuantityOnly, ct);

        if (instance == null)
        {
            instance = new ItemInstance
            {
                ItemId = item.Id,
                SerialNumber = snCode,
                Barcode = snCode,
                Status = ResolveInboundStatus(line.Status),
                DocumentNo = document.DocumentNo,
                TrackingType = ItemTrackingType.QuantityOnly,
                OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                IsActive = true,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.ItemInstances.Add(instance);
        }
        else
        {
            instance.Status = ResolveInboundStatus(line.Status);
            instance.DocumentNo = document.DocumentNo;
            instance.OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim();
        }

        // Stock Balance
        var delta = type switch
        {
            QuantityInventoryDocumentType.Issue => -line.Quantity,
            QuantityInventoryDocumentType.Adjust => 0,
            _ => line.Quantity
        };

        var balance = await _db.QuantityStockBalances.FirstOrDefaultAsync(x =>
            x.WarehouseId == request.WarehouseId &&
            x.ItemId == item.Id &&
            x.SnCode == snCode &&
            x.Status == ResolveInboundStatus(line.Status), ct);

        if (balance == null)
        {
            balance = new QuantityStockBalance
            {
                WarehouseId = request.WarehouseId,
                ItemId = item.Id,
                SnCode = snCode,
                Status = ResolveInboundStatus(line.Status),
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            };
            _db.QuantityStockBalances.Add(balance);
        }

        if (type == QuantityInventoryDocumentType.Issue && balance.Quantity < line.Quantity)
            return $"Insufficient quantity for item {item.ItemCode} SN {snCode}.";

        if (type != QuantityInventoryDocumentType.Adjust)
            balance.Quantity += delta;

        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;

        // Document Line
        document.Lines.Add(new QuantityInventoryDocumentLine
        {
            ItemId = item.Id,
            SnCode = snCode,
            Status = ResolveInboundStatus(line.Status),
            Quantity = line.Quantity,
            Note = line.Note,
            CreatedAt = request.DocumentDate,
            CreatedBy = user.UserName
        });

        // Transaction
        _db.QuantityInventoryTransactions.Add(new QuantityInventoryTransaction
        {
            TransactionType = type,
            WarehouseId = request.WarehouseId,
            ItemId = item.Id,
            SnCode = snCode,
            StatusAfter = ResolveInboundStatus(line.Status),
            QuantityDelta = delta,
            DocumentNo = document.DocumentNo,
            PostedAt = request.DocumentDate,
            PostedBy = user.UserName
        });

        return null;
    }

    private async Task<string?> ReverseDeletedLineAsync( QuantityInventoryDocumentLine oldLine,  QuantityInventoryDocument document, int warehouseId,
        QuantityInventoryDocumentType type,  CurrentUserContext user,  DateTime now,  CancellationToken ct)
    {
        var linesToDelete = await _db.QuantityInventoryDocumentLines
            .Where(x => x.QuantityInventoryDocumentId == document.Id && x.SnCode == oldLine.SnCode) .ToListAsync(ct);

        var transactionsToDelete = await _db.QuantityInventoryTransactions
            .Where(x => x.DocumentNo == document.DocumentNo && x.SnCode == oldLine.SnCode).ToListAsync(ct);

        var itemIntance = await _db.ItemInstances.FirstOrDefaultAsync(x => x.ItemId == oldLine.ItemId && x.SerialNumber == oldLine.SnCode, ct);

        if(itemIntance != null) _db.ItemInstances.Remove(itemIntance);
        _db.QuantityInventoryDocumentLines.RemoveRange(linesToDelete);
        _db.QuantityInventoryTransactions.RemoveRange(transactionsToDelete);


        var reverseDelta = type switch
        {
            QuantityInventoryDocumentType.Receive => -oldLine.Quantity,
            QuantityInventoryDocumentType.Issue => +oldLine.Quantity,
            _ => 0
        };

        var balance = await _db.QuantityStockBalances.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId &&
            x.ItemId == oldLine.ItemId &&
            x.SnCode == oldLine.SnCode &&
            x.Status == oldLine.Status, ct);

        if (balance != null)
        {
            balance.Quantity += reverseDelta;
            balance.UpdatedAt = now;
            balance.UpdatedBy = user.UserName;
        }

        return null;
    }

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

    // ─── Validation Helpers ───────────────────────────────────────────
    private static List<string> ValidateHeader(QuantityInventoryRequest request, CurrentUserContext user)
    {
        var errors = new List<string>();
        if (request.WarehouseId <= 0) errors.Add("Warehouse is required.");
        else if (!user.CanAccessWarehouse(request.WarehouseId)) errors.Add("Access denied for selected warehouse.");
        if (string.IsNullOrWhiteSpace(request.ItemCode)) errors.Add("ItemCode is required.");
        return errors;
    }

    private static bool IsEmptyLine(QuantityInventoryLineRequest line)
        => string.IsNullOrWhiteSpace(line.SnCode) && line.Quantity == 0;

    private IQueryable<QuantityStockBalance> ApplyReadScope(IQueryable<QuantityStockBalance> query, int? warehouseId, CurrentUserContext user)
    {
        if (warehouseId.HasValue)
        {
            return user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }

        return user.IsAdmin ? query : query.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
    }

    private static string NormalizeSn(string value) => value.Trim().ToUpperInvariant();

    // ─── Instance Detail Query ────────────────────────────────────────
    public async Task<IReadOnlyCollection<QuantityInstanceDto>> GetInstancesAsync(string? itemCode, int? warehouseId, string? ownerName, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode)) return Array.Empty<QuantityInstanceDto>();

        var normalizedCode = itemCode.Trim().ToUpperInvariant();

        // Get QuantityOnly instances for this item
        var instancesQuery = _db.ItemInstances
            .AsNoTracking()
            .Include(x => x.Item)
            .Where(x =>
                x.Item != null &&
                x.Item.ItemCode == normalizedCode &&
                x.TrackingType == ItemTrackingType.QuantityOnly &&
                x.IsActive);

        if (!string.IsNullOrWhiteSpace(ownerName))
        {
            var ownerFilter = ownerName.Trim();
            instancesQuery = instancesQuery.Where(x => x.OwnerName != null && x.OwnerName.Contains(ownerFilter));
        }

        var instances = await instancesQuery
            .Select(x => new { x.Id, x.SerialNumber, x.Status, x.TrackingType, x.OwnerName, x.CreatedAt, x.DocumentNo})
            .ToArrayAsync(cancellationToken);

        if (!instances.Any()) return Array.Empty<QuantityInstanceDto>();

        var snCodes = instances.Select(x => x.SerialNumber ?? string.Empty).Where(s => s.Length > 0).ToArray();

        // Get current balances (grouped by SN) for this item
        var balancesQuery = _db.QuantityStockBalances
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.Item != null && x.Item.ItemCode == normalizedCode && snCodes.Contains(x.SnCode));

        if (warehouseId.HasValue)
        {
            balancesQuery = user.CanAccessWarehouse(warehouseId.Value)
                ? balancesQuery.Where(x => x.WarehouseId == warehouseId.Value)
                : balancesQuery.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            balancesQuery = balancesQuery.Where(x => user.WarehouseIds.Contains(x.WarehouseId));
        }

        var balances = await balancesQuery
            .GroupBy(x => x.SnCode)
            .Select(g => new
            {
                SnCode = g.Key,
                TotalQty = g.Sum(b => b.Quantity),
                WarehouseCode = g.Select(b => b.Warehouse != null ? b.Warehouse.WarehouseCode : string.Empty).FirstOrDefault() ?? string.Empty
            })
            .ToDictionaryAsync(x => x.SnCode, cancellationToken);

        return instances.Select(inst =>
        {
            var sn = inst.SerialNumber ?? string.Empty;
            balances.TryGetValue(sn, out var bal);
            return new QuantityInstanceDto
            {
                Id = inst.Id,
                SnCode = sn,
                DocumentNo = inst.DocumentNo,
                Status = inst.Status.ToString(),
                TrackingType = inst.TrackingType.ToString(),
                WarehouseCode = bal?.WarehouseCode ?? string.Empty,
                Quantity = bal?.TotalQty ?? 0,
                OwnerName = inst.OwnerName,
                CreatedAt = inst.CreatedAt
            };
        }).OrderBy(x => x.SnCode).ToArray();
    }
}

