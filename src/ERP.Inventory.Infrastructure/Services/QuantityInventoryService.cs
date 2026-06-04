using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.GeometriesGraph;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace ERP.Inventory.Infrastructure.Services;


public sealed class QuantityInventoryService : InventoryOperationBase, IQuantityInventoryService
{
    private const string QuantityStockKey = "";
    private readonly ILogger<QuantityInventoryService> _logger;
    private readonly ILogErrorSystemService _errorLog;

    public QuantityInventoryService(
        InventoryDbContext db,
        IDocumentNumberService documentNumbers,
        IDateTimeProvider clock,
        ILogger<QuantityInventoryService> logger,
        ILogErrorSystemService errorLog)
        : base(db, documentNumbers, clock)
    {
        _logger = logger;
        _errorLog = errorLog;
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
                ItemCategoryCode = x.Item != null ? x.Item.Category.CategoryCode : string.Empty,
                ItemName = x.Item != null ? x.Item.DefaultName : string.Empty, x.WarehouseId,
                 WarehouseCode = x.Warehouse != null? x.Warehouse.WarehouseCode : string.Empty
             }).Select(g => new
             {
                 g.Key.ItemId, g.Key.ItemCode,
                 g.Key.ItemName, g.Key.WarehouseId,
                 g.Key.WarehouseCode, Quantity = g.Sum(x => x.Quantity),
                 ItemCategoryCode =g.Key.ItemCategoryCode,
                 LastUpdatedAt = g.Max(x => x.UpdatedAt ?? x.CreatedAt), Status = g.Select(x => x.Status).FirstOrDefault()
             }) //.OrderBy(x => x.WarehouseCode).ThenBy(x => x.ItemCode)
             .Skip((page - 1) * pageSize).Take(pageSize) .ToListAsync(cancellationToken);
                var itemIds = groupedRows.Select(x => x.ItemId).Distinct().ToList();
                //var ownerDict = await _db.ItemInstances .AsNoTracking() .Where(x => itemIds.Contains(x.ItemId) &&x.TrackingType == ItemTrackingType.QuantityOnly) .GroupBy(x => x.ItemId)
                //    .Select(g => new
                //    {
                //        ItemId = g.Key,OwnerName = g.Select(x => x.OwnerName).FirstOrDefault()
                //    }) .ToDictionaryAsync(x => x.ItemId, x => x.OwnerName, cancellationToken);
        var rows = groupedRows .Select(x => new QuantityStockBalanceDto
            {
                ItemId = x.ItemId, ItemCode = x.ItemCode, ItemCategoryCode = x.ItemCategoryCode,
                ItemName = x.ItemName, WarehouseId = x.WarehouseId,
                WarehouseCode = x.WarehouseCode, BinLocationId = null, BinCode = null, Quantity = x.Quantity,
                Status = x.Status.ToString(), LastUpdatedAt = x.LastUpdatedAt,
                OwnerName = "TE", /*ownerDict.TryGetValue(x.ItemId, out var owner) ? owner : null, */
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

        return await query .OrderByDescending(x => x.Id).Take(take)
            .Select(x => new QuantityInventoryTransactionDto
            {
                Id = x.Id, TransactionType = x.TransactionType.ToString(),
                DocumentNo = x.DocumentNo,
                ItemCategoryCode = x.Item.Category.CategoryCode,
                PostedAt = x.PostedAt,
                ItemCode = x.Item != null ? x.Item.ItemCode : string.Empty,
                BinLocationId = x.BinLocationId,
                BinCode = x.BinCode,
                SnCode = x.SnCode,   Status = x.StatusAfter.ToString(),
                QuantityDelta = x.QuantityDelta,  PostedBy = x.PostedBy
            }) .ToArrayAsync(cancellationToken);
    }

    // ─── Core Post Logic ─────────────────────────────────────────────
    private async Task<ServiceResult<PostedDocumentDto>> PostAsync(QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user, CancellationToken cancellationToken, bool isEdit = false)
    {
        try
        {
        if(isEdit == false) return await PostQuantityDocumentAsync(request, type, user, cancellationToken);

        var errors = ValidateHeader(request, user);
        var lines = request.Lines.Where(x => !IsEmptyLine(x)).ToArray();

        if (lines.Length == 0) errors.Add("At least one line is required.");
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var now = _clock.UtcNow;
        var documentNo = string.IsNullOrWhiteSpace(request.DocumentNo)? _documentNumbers.Next(type == QuantityInventoryDocumentType.Receive ? "QIN" :
            type == QuantityInventoryDocumentType.Issue ? "QOUT" : "QADJ", request.DocumentDate): request.DocumentNo.Trim();
        await using var tx = await BeginOperationTransactionAsync(cancellationToken);

        // ── LẤY DOCUMENT ─────────────────────────────────────────────
        var lifecycleBatchId = Guid.NewGuid();
        var existingDocument = await _db.QuantityInventoryDocuments.FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);
        var isNewDocument = existingDocument == null;
        QuantityInventoryDocument document;

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
            ApplyQuantityHeader(document, request, type, user);
            _db.QuantityInventoryDocuments.Add(document);
        }
        else
        {
            document = existingDocument!;
            _db.Entry(document).State = EntityState.Modified;
            document.DocumentDate = request.DocumentDate;
            document.PostedAt = request.DocumentDate;
            document.DocumentType = type;
            document.WarehouseId = request.WarehouseId;
            document.ApprovedBy = request.ApprovedBy;
            document.Note = request.Note;
            document.UpdatedAt = now;
            document.UpdatedBy = user.UserName;
            ApplyQuantityHeader(document, request, type, user);
        }

        var postingItems = await PreloadQuantityPostingItemsAsync(lines, request.DocumentDate, user, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SenderCode))
        {
            await GetOrCreatePartyByNameAsync(request.SenderName, request.SenderCode, ExternalPartyType.Borrower, "", request.SenderPhone, user.UserName, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            await GetOrCreatePartyByNameAsync(request.ReceiverName.Trim(), request.ReceiverCode, ExternalPartyType.Receiver, "", request.ReceiverPhone, user.UserName, now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── XỬ LÝ LINES ───────────────────────────────────────────────
        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var oldLines = isNewDocument ? new List<QuantityInventoryDocumentLine>(): await _db.QuantityInventoryDocumentLines
                .Where(x => x.QuantityInventoryDocumentId == document.Id).ToListAsync(cancellationToken);
        var oldLineGroups = oldLines
            .GroupBy(x => QuantityLineKey(x.ItemId, x.SnCode, x.BinLocationId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        var postingContext = await PreloadQuantityPostingContextAsync(lines, request.WarehouseId, postingItems.Values, oldLines, cancellationToken);

        foreach (var line in lines)
        {
            var item = ResolvePreloadedQuantityItem(line, postingItems);
            if (item == null)
            {
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<PostedDocumentDto>.Fail("ItemCode is required.");
            }

            var snCode = QuantityStockKey;

            var bin = ResolveInboundBin(line, postingContext);
            var lineKey = QuantityLineKey(item.Id, snCode, bin?.Id ?? line.BinLocationId);
            if (!incomingKeys.Add(lineKey))
            {
                errors.Add($"SN {snCode} is duplicated in this quantity document.");
                continue;
            }

            var existingLines = oldLineGroups.GetValueOrDefault(lineKey);
            var error = existingLines == null
                ? await ApplyQuantityLineAsync(document, line, item, type, request, user, now, lifecycleBatchId, existingLine: null, cancellationToken, postingContext)
                : await ReplaceQuantityLineAsync(document, existingLines, line, item, type, request, user, now, lifecycleBatchId, cancellationToken, postingContext);
            if (error != null) errors.Add(error);
        }

        // Xóa lines cũ không còn tồn tại (chỉ khi Edit)
        if (!isNewDocument)
        {
            foreach (var group in oldLineGroups)
            {
                if (incomingKeys.Contains(group.Key)) continue;

                var error = await RemoveQuantityLineAsync(group.Value, document, user, now, cancellationToken, postingContext);
                if (error != null) errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail(errors);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await CleanupZeroQuantityBalancesAsync(cancellationToken);
        // Backfill DocumentId
        foreach (var txRecord in _db.QuantityInventoryTransactions.Local.Where(x => x.DocumentNo == documentNo && x.DocumentId == 0))
        {
            txRecord.DocumentId = document.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);
        AddPostSideEffects(type.ToString(), nameof(QuantityInventoryDocument), document.Id, documentNo, user, Text(user.LanguageCode, "quantity_inventory_posted"));
        await tx.CommitAsync(cancellationToken);
        LogQuantityPosted(type, documentNo, request.WarehouseId, lines, postingItems, user);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto("QuantityInventory", document.Id, documentNo, now), Text(user.LanguageCode, "quantity_inventory_posted"));


        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Quantity inventory failed. DocumentNo={DocumentNo}",
                request.DocumentNo);

            var log = await _errorLog.LogAsync(ex, new LogErrorContext(
                Module: nameof(QuantityInventoryService),
                Action: $"Post:{type}",
                PayloadJson: JsonSerializer.Serialize(new
                {
                    request.DocumentNo,
                    request.WarehouseId,
                    Type = type.ToString(),
                    LineCount = request.Lines.Count
                }),
                UserId: user.UserId,
                UserName: user.UserName), CancellationToken.None);

            return ServiceResult<PostedDocumentDto>.Fail(SystemErrorMessage(user.LanguageCode, log.ErrorCode, ex));
        }
    }

    private async Task<ServiceResult<PostedDocumentDto>> PostQuantityDocumentAsync(QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var errors = ValidateHeader(request, user);
        var lines = request.Lines.Where(x => !IsEmptyLine(x)).ToArray();

        if (lines.Length == 0) errors.Add("At least one line is required.");
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var now = _clock.UtcNow;
        var documentNo = NormalizeQuantityDocumentNo(request.DocumentNo, type, request.DocumentDate);
        var lifecycleBatchId = Guid.NewGuid();

        await using var tx = await BeginOperationTransactionAsync(cancellationToken);

        var existingDocument = await _db.QuantityInventoryDocuments
            .FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);
        if (existingDocument != null && existingDocument.DocumentType != type)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail($"Document number {documentNo} already exists.");
        }

        var document = existingDocument ?? new QuantityInventoryDocument
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
        if (existingDocument != null)
        {
            document.DocumentDate = request.DocumentDate;
            document.PostedAt = request.DocumentDate;
            document.WarehouseId = request.WarehouseId;
            document.ApprovedBy = request.ApprovedBy;
            document.Note = request.Note;
            document.UpdatedAt = now;
            document.UpdatedBy = user.UserName;
        }
        ApplyQuantityHeader(document, request, type, user);
        if (existingDocument == null)
        {
            _db.QuantityInventoryDocuments.Add(document);
        }

        var postingItems = await PreloadQuantityPostingItemsAsync(lines, request.DocumentDate, user, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SenderCode))
        {
            await GetOrCreatePartyByNameAsync(request.SenderName, request.SenderCode, ExternalPartyType.Borrower, "", request.SenderPhone, user.UserName, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiverName))
        {
            await GetOrCreatePartyByNameAsync(request.ReceiverName.Trim(), request.ReceiverCode, ExternalPartyType.Receiver, "", request.ReceiverPhone, user.UserName, now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var postingContext = await PreloadQuantityPostingContextAsync(lines, request.WarehouseId, postingItems.Values, existingLines: null, cancellationToken);

        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var item = ResolvePreloadedQuantityItem(line, postingItems);
            if (item == null)
            {
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<PostedDocumentDto>.Fail("ItemCode is required.");
            }

            var bin = ResolveInboundBin(line, postingContext);
            var lineKey = QuantityLineKey(item.Id, QuantityStockKey, bin?.Id ?? line.BinLocationId);
            if (!incomingKeys.Add(lineKey))
            {
                errors.Add($"Item {item.ItemCode} is duplicated in this quantity document.");
                continue;
            }

            var error = await ApplyQuantityLineAsync(document, line, item, type, request, user, now, lifecycleBatchId, existingLine: null, cancellationToken, postingContext);
            if (error != null) errors.Add(error);
        }

        if (errors.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<PostedDocumentDto>.Fail(errors);
        }

        await CleanupZeroQuantityBalancesAsync(cancellationToken);
        AddPostSideEffects(type.ToString(), nameof(QuantityInventoryDocument), document.Id, documentNo, user, Text(user.LanguageCode, "quantity_inventory_posted"));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        LogQuantityPosted(type, documentNo, request.WarehouseId, lines, postingItems, user);

        return ServiceResult<PostedDocumentDto>.Ok(ToPostedDto("QuantityInventory", document.Id, documentNo, now), Text(user.LanguageCode, "quantity_inventory_posted"));
    }

    private async Task<ServiceResult<PostedDocumentDto>> PostNewAsync(QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var errors = ValidateHeader(request, user);
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var lines = request.Lines.Where(x => !IsEmptyLine(x)).ToArray();
        if (lines.Length == 0) errors.Add("At least one line is required.");
        if (errors.Count > 0) return ServiceResult<PostedDocumentDto>.Fail(errors);

        var now = _clock.UtcNow;
        var documentNo = NormalizeQuantityDocumentNo(request.DocumentNo, type, request.DocumentDate);

        // ── Begin transaction ─────────────────────────────────────────
        await using var tx = await BeginOperationTransactionAsync(cancellationToken);

        // ── Check duplicate DocumentNo ────────────────────────────────
        var document = await _db.QuantityInventoryDocuments.FirstOrDefaultAsync(x => x.DocumentNo == documentNo, cancellationToken);

        // ── Resolve/Create ItemCategory (once, outside line loop) ─────
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
            var normalizedCategoryCode = (line.ItemCategoryCode ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedItemCode = (line.ItemCode ?? string.Empty).Trim().ToUpperInvariant();

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
                errors.Add($"Insufficient quantity for item {item.ItemCode}.");
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

    private async Task<Dictionary<string, Item>> PreloadQuantityPostingItemsAsync(
        IReadOnlyCollection<QuantityInventoryLineRequest> lines,
        DateTime documentDate,
        CurrentUserContext user,
        CancellationToken ct)
    {
        var categoryCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var itemCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoryCodeByItemCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var itemCode = NormalizeQuantityLookupCode(line.ItemCode);
            if (string.IsNullOrWhiteSpace(itemCode)) continue;

            itemCodes.Add(itemCode);
            var categoryCode = NormalizeQuantityLookupCode(line.ItemCategoryCode);
            if (!string.IsNullOrWhiteSpace(categoryCode))
            {
                categoryCodes.Add(categoryCode);
                categoryCodeByItemCode.TryAdd(itemCode, categoryCode);
            }
        }

        var categories = categoryCodes.Count == 0
            ? new List<ItemCategory>()
            : await _db.ItemCategories
                .Where(x => categoryCodes.Contains(x.CategoryCode) && x.IsActive)
                .ToListAsync(ct);
        var categoriesByCode = categories.ToDictionary(x => x.CategoryCode, StringComparer.OrdinalIgnoreCase);

        foreach (var categoryCode in categoryCodes)
        {
            if (categoriesByCode.ContainsKey(categoryCode)) continue;

            var category = new ItemCategory
            {
                CategoryCode = categoryCode,
                Name = categoryCode,
                IsActive = true,
                CreatedAt = documentDate,
                CreatedBy = user.UserName
            };
            _db.ItemCategories.Add(category);
            categoriesByCode[categoryCode] = category;
        }

        var items = itemCodes.Count == 0
            ? new List<Item>()
            : await _db.Items
                .Where(x => itemCodes.Contains(x.ItemCode) && x.IsActive)
                .ToListAsync(ct);
        var itemsByCode = items.ToDictionary(x => x.ItemCode, StringComparer.OrdinalIgnoreCase);
        var missingItemCodes = itemCodes.Where(x => !itemsByCode.ContainsKey(x)).ToArray();
        var unit = missingItemCodes.Length == 0
            ? null
            : await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == "PCS", ct);

        foreach (var itemCode in missingItemCodes)
        {
            categoryCodeByItemCode.TryGetValue(itemCode, out var categoryCode);
            categoriesByCode.TryGetValue(categoryCode ?? string.Empty, out var category);

            var item = new Item
            {
                ItemCode = itemCode,
                DefaultName = itemCode,
                CategoryId = category?.Id ?? 0,
                Category = category,
                UnitId = unit?.Id ?? 0,
                Unit = unit,
                IsSerialManaged = false,
                IsActive = true,
                CreatedAt = documentDate,
                CreatedBy = user.UserName
            };
            _db.Items.Add(item);
            itemsByCode[itemCode] = item;
        }

        return itemsByCode;
    }

    private async Task<QuantityPostingContext> PreloadQuantityPostingContextAsync(
        IReadOnlyCollection<QuantityInventoryLineRequest> lines,
        int warehouseId,
        IEnumerable<Item> items,
        IReadOnlyCollection<QuantityInventoryDocumentLine>? existingLines,
        CancellationToken ct)
    {
        var itemIds = items.Select(x => x.Id)
            .Concat(existingLines?.Select(x => x.ItemId) ?? Array.Empty<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var statuses = lines.Select(x => ResolveInboundStatus(x.Status))
            .Concat(existingLines?.Select(x => x.Status) ?? Array.Empty<ItemStatus>())
            .Distinct()
            .ToArray();
        var binCodes = lines
            .Select(x => NormalizeLookupCode(x.BinCode))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedBinIds = lines
            .Where(x => x.BinLocationId.HasValue && x.BinLocationId.Value > 0)
            .Select(x => x.BinLocationId!.Value)
            .Distinct()
            .ToArray();
        var bins = binCodes.Length == 0 && requestedBinIds.Length == 0
          ? new List<BinLocation>()
          : await _db.BinLocations
              .AsNoTracking()
              .Where(x => x.IsActive &&
                  x.UsageType == BinLocationUsageType.Quantity &&
                  ((x.WarehouseId == warehouseId && binCodes.Contains(x.BinCode)) ||
                   requestedBinIds.Contains(x.Id)))
              .ToListAsync(ct);
        var binIds = bins.Select(x => x.Id).Distinct().ToArray();
        var occupiedBinIds = binIds.Length == 0
           ? new List<int>()
           : await _db.QuantityStockLocationBalances
               .AsNoTracking()
               .Where(x =>
                   binIds.Contains(x.BinLocationId) &&
                   x.Item != null &&
                   x.Item.IsActive )
               .Select(x => x.BinLocationId)
               .ToListAsync(ct);


        var balances = itemIds.Length == 0 || statuses.Length == 0
            ? new List<QuantityStockBalance>()
            : await _db.QuantityStockBalances
                .Where(x =>
                    x.WarehouseId == warehouseId &&
                    itemIds.Contains(x.ItemId) &&
                    x.SnCode == QuantityStockKey &&
                    statuses.Contains(x.Status))
                .ToListAsync(ct);

        var context = new QuantityPostingContext();
        foreach (var balance in balances.Concat(_db.QuantityStockBalances.Local))
        {
            var key = QuantityBalanceKey(balance.WarehouseId, balance.ItemId, balance.SnCode, balance.Status);
            context.Balances[key] = balance;
        }
        foreach (var bin in bins)
        {
            context.Bins[NormalizeLookupCode(bin.BinCode)] = bin;
            context.BinsById[bin.Id] = bin;
        }
        foreach (var binId in occupiedBinIds)
        {
            context.OccupiedBinIds.Add(binId);
        }
        foreach (var item in items)
        {
            context.Items[NormalizeLookupCode(item.ItemCode)] = item;
        }
        return context;
    }

    private static string NormalizeLookupCode(string? value)
       => (value ?? string.Empty).Trim();

    private static Item? ResolvePreloadedQuantityItem(QuantityInventoryLineRequest line, IReadOnlyDictionary<string, Item> itemsByCode)
    {
        var itemCode = NormalizeQuantityLookupCode(line.ItemCode);
        return string.IsNullOrWhiteSpace(itemCode) ? null : itemsByCode.GetValueOrDefault(itemCode);
    }

    private async Task<string?> ReplaceQuantityLineAsync( QuantityInventoryDocument document, List<QuantityInventoryDocumentLine> existingLines, QuantityInventoryLineRequest line, Item item,
        QuantityInventoryDocumentType type,  QuantityInventoryRequest request,  CurrentUserContext user,  DateTime now,  Guid? lifecycleBatchId, CancellationToken ct,
        QuantityPostingContext? postingContext = null)
    {
        var preservedLine = existingLines.OrderBy(x => x.Id).First();

        await ReverseQuantityEffectsAsync(document, existingLines, user, now, ct, postingContext);

        var duplicateLines = existingLines.Where(x => x.Id != preservedLine.Id).ToArray();
        if (duplicateLines.Length > 0)
        {
            _db.QuantityInventoryDocumentLines.RemoveRange(duplicateLines);
        }

        return await ApplyQuantityLineAsync(document, line, item, type, request, user, now, lifecycleBatchId, preservedLine, ct, postingContext);
    }

    private async Task<string?> RemoveQuantityLineAsync(  List<QuantityInventoryDocumentLine> existingLines, QuantityInventoryDocument document, CurrentUserContext user,  DateTime now,  CancellationToken ct,
        QuantityPostingContext? postingContext = null)
    {
        await ReverseQuantityEffectsAsync(document, existingLines, user, now, ct, postingContext);
        _db.QuantityInventoryDocumentLines.RemoveRange(existingLines);

        foreach (var line in existingLines)
        {
            await CleanupQuantityInstanceIfOrphanAsync(document.Id, line.ItemId, line.SnCode, ct);
        }

        return null;
    }

    private async Task<string?> ApplyQuantityLineAsync( QuantityInventoryDocument document, QuantityInventoryLineRequest line, Item item,  QuantityInventoryDocumentType type,
        QuantityInventoryRequest request, CurrentUserContext user, DateTime now, Guid? lifecycleBatchId,  QuantityInventoryDocumentLine? existingLine, CancellationToken ct,
        QuantityPostingContext? postingContext = null)
    {
        if (line.Quantity <= 0)
            return "Quantity must be greater than zero.";

        var snCode = QuantityStockKey;

        var status = ResolveInboundStatus(line.Status);

        var bin = ResolveInboundBin(line, postingContext);
        if (bin == null)
        {
            var message = ResolveBinValidationMessage(line, user);
            LogQuantityValidationWarning(document.DocumentNo, request.WarehouseId, line.BinLocationId, item.Id, message);
            return message;
        }

        if (type == QuantityInventoryDocumentType.Adjust)
        {
            var error = await ApplyQuantityAdjustmentAsync(document, request, line, item, snCode, status, line.Quantity, user, now, lifecycleBatchId, ct, postingContext);
            if (error != null) return error;
        }
        else
        {
            var delta = type == QuantityInventoryDocumentType.Issue ? -line.Quantity : line.Quantity;
            var balance = postingContext == null
                ? await GetOrCreateQuantityBalanceAsync(request.WarehouseId, item.Id, snCode, status, request.DocumentDate, user, ct)
                : GetOrCreateQuantityBalance(postingContext, request.WarehouseId, item.Id, snCode, status, request.DocumentDate, user);

            if (type == QuantityInventoryDocumentType.Issue && balance.Quantity < line.Quantity)
                return $"Insufficient quantity for item {item.ItemCode}.";

            balance.Quantity += delta;
            balance.UpdatedAt = now;
            balance.UpdatedBy = user.UserName;

            var locationResult = await ApplyQuantityLocationDeltaAsync(document.DocumentNo, request.WarehouseId, bin.Id, item.Id, status, delta, request.DocumentDate, user, now, ct);
            if (locationResult.Error != null) return locationResult.Error;

            AddQuantityTransaction(document, type, request.WarehouseId, item.Id, bin.Id, locationResult.BinCode, snCode, status, delta, request.DocumentDate, user, lifecycleBatchId);
        }

        if (existingLine == null)
        {
            document.Lines.Add(new QuantityInventoryDocumentLine
            {
                ItemId = item.Id,
                BinLocationId = bin.Id,
                SnCode = snCode,
                Status = status,
                Quantity = line.Quantity,
                LifecycleBatchId = lifecycleBatchId,
                Note = line.Note,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            });
        }
        else
        {
            existingLine.ItemId = item.Id;
            existingLine.BinLocationId = bin.Id;
            existingLine.SnCode = snCode;
            existingLine.Status = status;
            existingLine.Quantity = line.Quantity;
            existingLine.LifecycleBatchId = lifecycleBatchId;
            existingLine.Note = line.Note;
            existingLine.UpdatedAt = now;
            existingLine.UpdatedBy = user.UserName;
        }

        return null;
    }

    private async Task<string?> ApplyQuantityAdjustmentAsync(  QuantityInventoryDocument document,  QuantityInventoryRequest request, QuantityInventoryLineRequest line,Item item,  string snCode,
        ItemStatus targetStatus, decimal quantity,  CurrentUserContext user, DateTime now, Guid? lifecycleBatchId, CancellationToken ct, QuantityPostingContext? postingContext = null)
    {
        var delta = ResolveAdjustmentDelta(request, line, quantity);
        var balance = postingContext == null
            ? await GetOrCreateQuantityBalanceAsync(request.WarehouseId, item.Id, snCode, targetStatus, request.DocumentDate, user, ct)
            : GetOrCreateQuantityBalance(postingContext, request.WarehouseId, item.Id, snCode, targetStatus, request.DocumentDate, user);

        if (delta < 0 && balance.Quantity < Math.Abs(delta))
            return $"Insufficient quantity for item {item.ItemCode}.";

        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;

        var bin = ResolveInboundBin(line, postingContext);
        if (bin == null)
        {
            var message = ResolveBinValidationMessage(line, user);
            LogQuantityValidationWarning(document.DocumentNo, request.WarehouseId, line.BinLocationId, item.Id, message);
            return message;
        }

        var locationResult = await ApplyQuantityLocationDeltaAsync(document.DocumentNo, request.WarehouseId, bin.Id, item.Id, targetStatus, delta, request.DocumentDate, user, now, ct);
        if (locationResult.Error != null) return locationResult.Error;

        AddQuantityTransaction(document, QuantityInventoryDocumentType.Adjust, request.WarehouseId, item.Id, bin.Id, locationResult.BinCode, snCode, targetStatus, delta, request.DocumentDate, user, lifecycleBatchId);
        return null;
    }

    private async Task<ServiceResult<BinLocation>> ValidateBinLocationAsync(int warehouseId, int? binLocationId, CurrentUserContext user, CancellationToken ct)
    {
        if (!binLocationId.HasValue || binLocationId.Value <= 0)
        {
            return ServiceResult<BinLocation>.Fail(Text(user.LanguageCode, "quantity_location_required"));
        }

        var bin = _db.BinLocations.Local.FirstOrDefault(x =>
            x.Id == binLocationId.Value &&
            x.UsageType == BinLocationUsageType.Quantity);
        if (bin == null)
        {
            bin = await _db.BinLocations.FirstOrDefaultAsync(x =>
                x.Id == binLocationId.Value &&
                x.UsageType == BinLocationUsageType.Quantity, ct);
        }

        if (bin == null || !bin.IsActive)
        {
            return ServiceResult<BinLocation>.Fail(Text(user.LanguageCode, "quantity_location_invalid"));
        }

        if (bin.WarehouseId != warehouseId)
        {
            return ServiceResult<BinLocation>.Fail(Text(user.LanguageCode, "quantity_location_wrong_warehouse"));
        }

        return ServiceResult<BinLocation>.Ok(bin);
    }

    private async Task<QuantityStockLocationBalance> GetOrCreateQuantityLocationBalanceAsync(
        int warehouseId,
        int binLocationId,
        int itemId,
        ItemStatus status,
        DateTime createdAt,
        CurrentUserContext user,
        CancellationToken ct)
    {
        var balance = _db.QuantityStockLocationBalances.Local.FirstOrDefault(x =>
            x.WarehouseId == warehouseId &&
            x.BinLocationId == binLocationId &&
            x.ItemId == itemId &&
            x.Status == status);

        if (balance == null)
        {
            balance = await _db.QuantityStockLocationBalances.FirstOrDefaultAsync(x =>
                x.WarehouseId == warehouseId &&
                x.BinLocationId == binLocationId &&
                x.ItemId == itemId &&
                x.Status == status, ct);
        }

        if (balance != null)
        {
            return balance;
        }

        balance = new QuantityStockLocationBalance
        {
            WarehouseId = warehouseId,
            BinLocationId = binLocationId,
            ItemId = itemId,
            Status = status,
            Quantity = 0,
            CreatedAt = createdAt,
            CreatedBy = user.UserName
        };
        _db.QuantityStockLocationBalances.Add(balance);
        return balance;
    }

    private async Task<QuantityStockLocationBalance?> FindQuantityLocationBalanceAsync(int warehouseId, int binLocationId, int itemId, ItemStatus status, CancellationToken ct)
    {
        var balance = _db.QuantityStockLocationBalances.Local.FirstOrDefault(x =>
            x.WarehouseId == warehouseId &&
            x.BinLocationId == binLocationId &&
            x.ItemId == itemId &&
            x.Status == status);

        return balance ?? await _db.QuantityStockLocationBalances.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId &&
            x.BinLocationId == binLocationId &&
            x.ItemId == itemId &&
            x.Status == status, ct);
    }

    private async Task<QuantityLocationDeltaResult> ApplyQuantityLocationDeltaAsync(
        string documentNo,
        int warehouseId,
        int? binLocationId,
        int itemId,
        ItemStatus status,
        decimal delta,
        DateTime createdAt,
        CurrentUserContext user,
        DateTime now,
        CancellationToken ct)
    {
        var validation = await ValidateBinLocationAsync(warehouseId, binLocationId, user, ct);
        if (!validation.Success || validation.Data == null)
        {
            LogQuantityValidationWarning(documentNo, warehouseId, binLocationId, itemId, validation.Message);
            return QuantityLocationDeltaResult.Fail(validation.Message);
        }

        var bin = validation.Data;
        QuantityStockLocationBalance balance;

        if (delta < 0)
        {
            balance = await FindQuantityLocationBalanceAsync(warehouseId, bin.Id, itemId, status, ct)
                ?? null!;

            if (balance == null)
            {
                var message = Text(user.LanguageCode, "quantity_location_item_not_found");
                LogQuantityValidationWarning(documentNo, warehouseId, binLocationId, itemId, message);
                return QuantityLocationDeltaResult.Fail(message);
            }

            if (balance.Quantity < Math.Abs(delta))
            {
                var message = Text(user.LanguageCode, "quantity_location_insufficient");
                LogQuantityValidationWarning(documentNo, warehouseId, binLocationId, itemId, message);
                return QuantityLocationDeltaResult.Fail(message);
            }
        }
        else
        {
            balance = await GetOrCreateQuantityLocationBalanceAsync(warehouseId, bin.Id, itemId, status, createdAt, user, ct);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;

        return QuantityLocationDeltaResult.Ok(bin.BinCode);
    }

    private async Task ReverseQuantityEffectsAsync( QuantityInventoryDocument document,  IReadOnlyCollection<QuantityInventoryDocumentLine> existingLines, CurrentUserContext user,  DateTime now, CancellationToken ct,
        QuantityPostingContext? postingContext = null)
    {
        foreach (var group in existingLines.GroupBy(x => QuantityLineKey(x.ItemId, x.SnCode, x.BinLocationId), StringComparer.OrdinalIgnoreCase))
        {
            var line = group.First();
            var snCode = NormalizeSn(line.SnCode);
            var txs = await _db.QuantityInventoryTransactions
                .Where(x => x.DocumentId == document.Id && x.ItemId == line.ItemId && x.SnCode == snCode && x.BinLocationId == line.BinLocationId)
                .ToListAsync(ct);

            if (txs.Count == 0)
            {
                var quantity = group.Sum(x => x.Quantity);
                var fallbackDelta = document.DocumentType switch
                {
                    QuantityInventoryDocumentType.Receive => quantity,
                    QuantityInventoryDocumentType.Issue => -quantity,
                    _ => 0
                };

                if (fallbackDelta != 0)
                {
                    await ApplyQuantityBalanceDeltaAsync(document.WarehouseId, line.ItemId, snCode, line.Status, -fallbackDelta, user, now, ct, postingContext);
                    if (line.BinLocationId.HasValue)
                    {
                        await ApplyQuantityLocationBalanceDeltaAsync(document.WarehouseId, line.BinLocationId.Value, line.ItemId, line.Status, -fallbackDelta, user, now, ct);
                    }
                }

                continue;
            }

            foreach (var tx in txs)
            {
                if (tx.QuantityDelta != 0)
                {
                    await ApplyQuantityBalanceDeltaAsync(tx.WarehouseId, tx.ItemId, tx.SnCode, tx.StatusAfter, -tx.QuantityDelta, user, now, ct, postingContext);
                    if (tx.BinLocationId.HasValue)
                    {
                        await ApplyQuantityLocationBalanceDeltaAsync(tx.WarehouseId, tx.BinLocationId.Value, tx.ItemId, tx.StatusAfter, -tx.QuantityDelta, user, now, ct);
                    }
                }
            }

            _db.QuantityInventoryTransactions.RemoveRange(txs);
        }
    }

    private async Task ApplyQuantityBalanceDeltaAsync( int warehouseId,  int itemId,string snCode, ItemStatus status, decimal delta, CurrentUserContext user,  DateTime now,  CancellationToken ct,
        QuantityPostingContext? postingContext = null)
    {
        var balance = postingContext == null
            ? await GetOrCreateQuantityBalanceAsync(warehouseId, itemId, snCode, status, now, user, ct)
            : GetOrCreateQuantityBalance(postingContext, warehouseId, itemId, snCode, status, now, user);
        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;
    }

    private async Task ApplyQuantityLocationBalanceDeltaAsync(int warehouseId, int binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, DateTime now, CancellationToken ct)
    {
        var balance = await GetOrCreateQuantityLocationBalanceAsync(warehouseId, binLocationId, itemId, status, now, user, ct);
        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;
    }

    private async Task<QuantityStockBalance> GetOrCreateQuantityBalanceAsync( int warehouseId,  int itemId,  string snCode,  ItemStatus status, DateTime createdAt, CurrentUserContext user,  CancellationToken ct)
    {
        var balance = _db.QuantityStockBalances.Local.FirstOrDefault(x =>
            x.WarehouseId == warehouseId &&
            x.ItemId == itemId &&
            x.SnCode == snCode &&
            x.Status == status);

        if (balance == null)
        {
            balance = await _db.QuantityStockBalances.FirstOrDefaultAsync(x =>
                x.WarehouseId == warehouseId &&
                x.ItemId == itemId &&
                x.SnCode == snCode &&
                x.Status == status, ct);
        }

        if (balance != null)
        {
            return balance;
        }

        balance = new QuantityStockBalance
        {
            WarehouseId = warehouseId,
            ItemId = itemId,
            SnCode = snCode,
            Status = status,
            Quantity = 0,
            CreatedAt = createdAt,
            CreatedBy = user.UserName
        };
        _db.QuantityStockBalances.Add(balance);
        return balance;
    }

    private QuantityStockBalance GetOrCreateQuantityBalance(
        QuantityPostingContext context,
        int warehouseId,
        int itemId,
        string snCode,
        ItemStatus status,
        DateTime createdAt,
        CurrentUserContext user)
    {
        var key = QuantityBalanceKey(warehouseId, itemId, snCode, status);
        if (context.Balances.TryGetValue(key, out var balance))
        {
            return balance;
        }

        balance = new QuantityStockBalance
        {
            WarehouseId = warehouseId,
            ItemId = itemId,
            SnCode = snCode,
            Status = status,
            Quantity = 0,
            CreatedAt = createdAt,
            CreatedBy = user.UserName
        };
        _db.QuantityStockBalances.Add(balance);
        context.Balances[key] = balance;
        return balance;
    }

    private void AddQuantityTransaction( QuantityInventoryDocument document,  QuantityInventoryDocumentType type,  int warehouseId,   int itemId,  int? binLocationId, string binCode, string snCode, 
        ItemStatus status, decimal delta,  DateTime postedAt,  CurrentUserContext user,  Guid? lifecycleBatchId)
    {
        _db.QuantityInventoryTransactions.Add(new QuantityInventoryTransaction
        {
            TransactionType = type,
            WarehouseId = warehouseId,
            ItemId = itemId,
            BinLocationId = binLocationId,
            BinCode = binCode,
            SnCode = snCode,
            StatusAfter = status,
            QuantityDelta = delta,
            DocumentId = document.Id,
            DocumentNo = document.DocumentNo,
            LifecycleBatchId = lifecycleBatchId,
            PostedAt = postedAt,
            PostedBy = user.UserName,
            ReceiverCode = document.ReceiverCode.Trim() ?? string.Empty,
            ReceiverName = document.ReceiverName.Trim() ?? string.Empty,
            ReceiverPhone = document.ReceiverPhone.Trim() ?? string.Empty,
            SenderCode = document.SenderCode.Trim() ??  string.Empty,
            SenderName = document.SenderName.Trim() ?? string.Empty,
            SenderPhone = document.SenderPhone.Trim() ?? string.Empty,
        });
    }

    private async Task CleanupQuantityInstanceIfOrphanAsync(int documentId, int itemId, string snCode, CancellationToken ct)
    {
        await Task.CompletedTask;
    }

    private async Task CleanupZeroQuantityBalancesAsync(CancellationToken ct)
    {
        var localZeroBalances = _db.QuantityStockBalances.Local
            .Where(x => x.Quantity == 0)
            .ToArray();

        if (localZeroBalances.Length > 0)
        {
            _db.QuantityStockBalances.RemoveRange(localZeroBalances);
        }

        var zeroBalances = await _db.QuantityStockBalances
            .Where(x => x.Quantity == 0)
            .ToListAsync(ct);

        if (zeroBalances.Count > 0)
        {
            _db.QuantityStockBalances.RemoveRange(zeroBalances);
        }

        var localZeroLocationBalances = _db.QuantityStockLocationBalances.Local
            .Where(x => x.Quantity == 0)
            .ToArray();

        if (localZeroLocationBalances.Length > 0)
        {
            _db.QuantityStockLocationBalances.RemoveRange(localZeroLocationBalances);
        }

        var zeroLocationBalances = await _db.QuantityStockLocationBalances
            .Where(x => x.Quantity == 0)
            .ToListAsync(ct);

        if (zeroLocationBalances.Count > 0)
        {
            _db.QuantityStockLocationBalances.RemoveRange(zeroLocationBalances);
        }
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
            return $"Insufficient quantity for item {item.ItemCode}.";

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
    private async Task<Item?> ResolveOrCreateItemAsync(DateTime documentDate, QuantityInventoryLineRequest line, CurrentUserContext user, CancellationToken ct)
    {
        var normalizedCategoryCode = (line.ItemCategoryCode ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedItemCode = (line.ItemCode ?? string.Empty).Trim().ToUpperInvariant();

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
                    CreatedAt = documentDate,
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
                    CreatedAt = documentDate,
                    CreatedBy = user.UserName
                };
                _db.Items.Add(item);
                await _db.SaveChangesAsync(ct);
            }
        }
        return item;
    }

    private string NormalizeQuantityDocumentNo(string? documentNo, QuantityInventoryDocumentType type, DateTime documentDate)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            return _documentNumbers.Next(type == QuantityInventoryDocumentType.Receive ? "QIN" :
                type == QuantityInventoryDocumentType.Issue ? "QOUT" : "QADJ", documentDate);
        }

        var prefix = type == QuantityInventoryDocumentType.Receive ? "QIN" :
            type == QuantityInventoryDocumentType.Issue ? "QOUT" : "QADJ";
        var trimmed = documentNo.Trim();
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : prefix + trimmed;
    }

    private static void ApplyQuantityHeader(QuantityInventoryDocument document, QuantityInventoryRequest request, QuantityInventoryDocumentType type, CurrentUserContext user)
    {
        document.OperatorUserId = user.UserId;
        document.OperatorUserCode = string.IsNullOrWhiteSpace(request.OperatorUserCode) ? user.UserId : request.OperatorUserCode.Trim();
        document.OperatorUserName = string.IsNullOrWhiteSpace(request.OperatorUserName) ? user.UserName : request.OperatorUserName.Trim();

        if (type == QuantityInventoryDocumentType.Receive)
        {
            document.SenderCode = (request.SenderCode ?? string.Empty).Trim();
            document.SenderName = (request.SenderName ?? string.Empty).Trim();
            document.SenderPhone = (request.SenderPhone ?? string.Empty).Trim();
            document.ReceiverCode = string.Empty;
            document.ReceiverName = string.Empty;
            document.ReceiverPhone = string.Empty;
        }
        else
        {
            document.ReceiverCode = (request.ReceiverCode ?? string.Empty).Trim();
            document.ReceiverName = (request.ReceiverName ?? string.Empty).Trim();
            document.ReceiverPhone = (request.ReceiverPhone ?? string.Empty).Trim();
            document.SenderCode = string.Empty;
            document.SenderName = string.Empty;
            document.SenderPhone = string.Empty;
        }
    }
    private static BinLocation? ResolveInboundBin(QuantityInventoryLineRequest line, QuantityPostingContext? context)
    {
        if (context == null)
        {
            return null;
        }

        var binCode = NormalizeLookupCode(line.BinCode);
        if (binCode.Length > 0 && context.Bins.TryGetValue(binCode, out var byCode))
        {
            return byCode;
        }

        return line.BinLocationId.HasValue && context.BinsById.TryGetValue(line.BinLocationId.Value, out var byId)
            ? byId
            : null;
    }

    private static decimal ResolveAdjustmentDelta(QuantityInventoryRequest request, QuantityInventoryLineRequest line, decimal quantity)
    {
        var direction = string.IsNullOrWhiteSpace(line.AdjustmentDirection)
            ? request.AdjustmentDirection
            : line.AdjustmentDirection;
        return IsDecrease(direction) ? -quantity : quantity;
    }

    private static bool IsDecrease(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized.Equals("Decrease", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("AdjustmentDecrease", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Out", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Minus", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("-", StringComparison.OrdinalIgnoreCase);
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
            return $"Insufficient quantity for item {item.ItemCode}.";

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
        foreach(var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode)) errors.Add("ItemCode is required.");
        }
        return errors;
    }

    private static bool IsEmptyLine(QuantityInventoryLineRequest line)
        => string.IsNullOrWhiteSpace(line.ItemCode) && line.Quantity == 0;

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

    private static string NormalizeQuantityLookupCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeQuantityCode(string? snCode, string itemCode)
        => QuantityStockKey;

    private static string QuantityLineKey(int itemId, string snCode)
        => QuantityLineKey(itemId, snCode, null);

    private static string QuantityLineKey(int itemId, string snCode, int? binLocationId)
        => $"{itemId}:{NormalizeSn(snCode)}:{binLocationId?.ToString() ?? string.Empty}";

    private static string QuantityBalanceKey(int warehouseId, int itemId, string snCode, ItemStatus status)
        => $"{warehouseId}:{itemId}:{NormalizeSn(snCode)}:{(int)status}";

    private void LogQuantityValidationWarning(string documentNo, int warehouseId, int? binLocationId, int itemId, string reason)
    {
        _logger.LogWarning(
            "Quantity issue validation failed. DocumentNo={DocumentNo}, Warehouse={WarehouseId}, Bin={BinLocationId}, Item={ItemId}, Reason={Reason}",
            documentNo,
            warehouseId,
            binLocationId,
            itemId,
            reason);
    }

    private void LogQuantityPosted(QuantityInventoryDocumentType type, string documentNo, int warehouseId, IReadOnlyCollection<QuantityInventoryLineRequest> lines, IReadOnlyDictionary<string, Item> itemsByCode, CurrentUserContext user)
    {
        foreach (var line in lines)
        {
            var item = ResolvePreloadedQuantityItem(line, itemsByCode);
            _logger.LogInformation(
                "Quantity inventory posted. Type={Type}, DocumentNo={DocumentNo}, Warehouse={WarehouseId}, Item={ItemId}, Bin={BinLocationId}, Qty={Qty}, User={User}",
                type,
                documentNo,
                warehouseId,
                item?.Id,
                line.BinCode,
                line.Quantity,
                user.UserName);
        }
    }

    private static string ResolveBinValidationMessage(QuantityInventoryLineRequest line, CurrentUserContext user)
        => string.IsNullOrWhiteSpace(line.BinCode) && !line.BinLocationId.HasValue
            ? Text(user.LanguageCode, "quantity_location_required")
            : Text(user.LanguageCode, "quantity_location_invalid");

    private static string SystemErrorMessage(string? language, string errorCode, Exception? exception = null)
    {
        if (IsTimeout(exception))
        {
            return language?.ToLowerInvariant() switch
            {
                "en" => $"The system is taking too long to respond or is overloaded. Error code: {errorCode}. Please try the operation again.",
                "zh" => $"系统响应时间过长或负载过高。错误代码：{errorCode}。请稍后重试该操作。",
                _ => $"Hệ thống phản hồi chậm hoặc đang quá tải. Mã lỗi: {errorCode}. Vui lòng thử lại thao tác sau."
            };
        }

        return language?.ToLowerInvariant() switch
        {
            "en" => $"System error occurred. Error code: {errorCode}. Please contact TE/IT.",
            "zh" => $"系统发生错误。错误代码：{errorCode}。请联系 TE/IT 获取支持。",
            _ => $"Có lỗi hệ thống. Mã lỗi: {errorCode}. Vui lòng liên hệ TE/IT."
        };
    }

    private static bool IsTimeout(Exception? exception)
    {
        if (exception == null) return false;
        if (exception is TimeoutException or TaskCanceledException or OperationCanceledException) return true;

        var typeName = exception.GetType().FullName ?? exception.GetType().Name;
        if (typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
            && exception.GetType().GetProperty("Number")?.GetValue(exception) is int number
            && number == -2)
        {
            return true;
        }

        return IsTimeout(exception.InnerException);
    }

    private static string Text(string language, string key)
    {
        var resources = language switch
        {
            "en" => En,
            "zh" => En,
            _ => Vi
        };

        return resources.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly Dictionary<string, string> Vi = new()
    {
        ["quantity_location_required"] = "Vị trí là bắt buộc.",
        ["quantity_location_invalid"] = "Không tìm thấy vị trí.",
        ["quantity_location_wrong_warehouse"] = "Vị trí không thuộc kho đã chọn.",
        ["quantity_location_item_not_found"] = "Mặt hàng không tồn tại tại vị trí này.",
        ["quantity_location_insufficient"] = "Không đủ số lượng tại vị trí.",
        ["quantity_inventory_posted"] = "Đã ghi sổ tồn kho số lượng.",
        ["quantity_inventory_failed"] = "Thao tác tồn số lượng thất bại."
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["quantity_location_required"] = "Location is required.",
        ["quantity_location_invalid"] = "Location does not exist.",
        ["quantity_location_wrong_warehouse"] = "Selected location does not belong to warehouse.",
        ["quantity_location_item_not_found"] = "Item does not exist at selected location.",
        ["quantity_location_insufficient"] = "Insufficient quantity at selected location.",
        ["quantity_inventory_posted"] = "Quantity inventory posted.",
        ["quantity_inventory_failed"] = "Quantity inventory operation failed."
    };

    private sealed class QuantityPostingContext
    {
        public Dictionary<string, QuantityStockBalance> Balances { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BinLocation> Bins { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, BinLocation> BinsById { get; } = new();
        public HashSet<int> OccupiedBinIds { get; } = new();
        public Dictionary<string, Item> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class QuantityLocationDeltaResult
    {
        public string? Error { get; private init; }
        public string BinCode { get; private init; } = string.Empty;

        public static QuantityLocationDeltaResult Ok(string binCode)
            => new() { BinCode = binCode };

        public static QuantityLocationDeltaResult Fail(string error)
            => new() { Error = error };
    }

    // ─── Instance Detail Query ────────────────────────────────────────
    public async Task<IReadOnlyCollection<QuantityStockBalanceDto>> GetDetailsAsync(string? itemCode, int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return Array.Empty<QuantityStockBalanceDto>();

        var normalizedCode = itemCode.Trim().ToUpperInvariant();

        var query = _db.QuantityInventoryTransactions
            .AsNoTracking()
            .Include(x => x.Item)!.ThenInclude(x => x!.Category)
            .Include(x => x.Warehouse)
            .Where(x =>
                x.Item != null &&
                x.Item.ItemCode == normalizedCode);
        if (warehouseId.HasValue)
        {
            query = user.CanAccessWarehouse(warehouseId.Value)
                ? query.Where(x => x.WarehouseId == warehouseId.Value)
                : query.Where(x => false);
        }
        else if (!user.IsAdmin)
        {
            query = query.Where(x =>
                user.WarehouseIds.Contains(x.WarehouseId));
        }

        var data = await query
            .OrderByDescending(x => x.PostedAt)
            .Select(x => new
            {
                x.ItemId,
                ItemCode = x.Item != null ? x.Item.ItemCode : "",
                ItemName = x.Item != null ? x.Item.DefaultName : "",
                ItemCategoryCode =x.Item != null && x.Item.Category != null
                        ? x.Item.Category.CategoryCode : "",
                x.WarehouseId,
                actionTypeText =$"Enum.QuantityInventoryDocumentType.{x.TransactionType}",
                WarehouseCode = x.Warehouse != null  ? x.Warehouse.WarehouseCode
                        : "",
                x.BinLocationId,
                x.BinCode,
                Status = x.StatusAfter,
                Quantity = x.QuantityDelta,
                LastUpdatedAt = x.PostedAt,
                x.DocumentNo,
                receiver = string.IsNullOrWhiteSpace(x.ReceiverCode) && string.IsNullOrWhiteSpace(x.ReceiverName) ? null
                            : $"{x.ReceiverCode}-{x.ReceiverName}",

                sender = string.IsNullOrWhiteSpace(x.SenderCode) && string.IsNullOrWhiteSpace(x.SenderName) ? null
                            : $"{x.SenderCode}-{x.SenderName}",
                department = "TE",
                oldLocation = x.QuantityDelta > 0 ? x.Warehouse.Name : "",
                receiverPhone = string.IsNullOrWhiteSpace(x.ReceiverPhone) ? x.SenderPhone : x.ReceiverPhone,
                performedBy = x.PostedBy,
                timestamp = x.PostedAt
            })
            .ToListAsync(cancellationToken);

        var result = data.Select(x => new QuantityStockBalanceDto
        {
            Timestamp = x.timestamp,
            Action = x.actionTypeText,
            ItemId = x.ItemId,
            ItemCode = x.ItemCode,
            ItemName = x.ItemName,
            ItemCategoryCode = x.ItemCategoryCode,
            WarehouseId = x.WarehouseId,
            WarehouseCode = string.IsNullOrWhiteSpace(x.BinCode) ? x.oldLocation : x.BinCode,
            BinLocationId = x.BinLocationId,
            BinCode = x.BinCode,
            Status = x.Status.ToString(),
            Quantity = x.Quantity,
            Receiver = x.receiver,
            Sender = x.sender,
            ReceiverPhone = x.receiverPhone,
            ApprovedBy = x.performedBy,
            ReceiverDepartment = x.department,
            LastUpdatedAt = x.LastUpdatedAt
        }).ToList();

        return result;
    }
}

