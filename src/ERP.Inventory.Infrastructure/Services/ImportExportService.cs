using EFCore.BulkExtensions;
using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;
using System;
using System.Text.Json;
using System.Xml.Linq;
using NetTopologySuite.Index.HPRtree;
using SQLitePCL;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class ImportExportService : IImportService, IExportService
{
    private const int MaxStoredOriginalFileBytes = 20 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly InventoryDbContext _db;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IInboundService _inboundService;
    private readonly IQuantityInventoryService _quantityInventoryService;
    private readonly IInventoryOperationService _moveLocationService;
    private readonly IBorrowService _borrowService;
    private readonly IRepairService _repairService;
    private readonly InventoryCheckService _inventoryCheckService;

    public ImportExportService(
        InventoryDbContext db,
        IDocumentNumberService documentNumbers,
        IInboundService inboundService,
        IQuantityInventoryService quantityInventoryService,
        IInventoryOperationService moveLocationService,
        IBorrowService borrowService,
        IRepairService repairService,
        InventoryCheckService inventoryCheckService)
    {
        _db = db;
        _documentNumbers = documentNumbers;
        _inboundService = inboundService;
        _quantityInventoryService = quantityInventoryService;
        _moveLocationService = moveLocationService;
        _borrowService = borrowService;
        _repairService = repairService;
        _inventoryCheckService = inventoryCheckService;
    }

    private sealed class InventoryCheckImportContext
    {
        public Dictionary<string, Warehouse> Warehouses { get; }
        public Dictionary<string, Item> ItemsByCode { get; }
        public Dictionary<string, ItemInstance> InstancesBySerial { get; }
        public Dictionary<int, CurrentItemLocation> LocationsByInstanceId { get; }
        public Dictionary<(int WarehouseId, string BinCode), BinLocation> Bins { get; }

        public InventoryCheckImportContext(
            Dictionary<string, Warehouse> warehouses,
            Dictionary<string, Item> itemsByCode,
            Dictionary<string, ItemInstance> instancesBySerial,
            Dictionary<int, CurrentItemLocation> locationsByInstanceId,
            Dictionary<(int WarehouseId, string BinCode), BinLocation> bins)
        {
            Warehouses = warehouses;
            ItemsByCode = itemsByCode;
            InstancesBySerial = instancesBySerial;
            LocationsByInstanceId = locationsByInstanceId;
            Bins = bins;
        }
    }

    private sealed class ImportValidationContext
    {
        public Dictionary<string, Item> ActiveItemsByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingItemCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Warehouse> ActiveWarehousesByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(int WarehouseId, string BinCode), BinLocation> ActiveBinsByWarehouseAndCode { get; } = new();
        public HashSet<(int WarehouseId, string BinCode)> ExistingBinKeys { get; } = new();
        public Dictionary<(string ItemCode, string SerialNumber), ItemInstance> InstancesByItemAndSerial { get; } = new();
        public HashSet<(string ItemCode, string SerialNumber)> DeletedInstanceKeys { get; } = new();
        public Dictionary<string, ItemInstance> InstancesBySerialOrBarcode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, CurrentItemLocation> CurrentLocationsByInstanceId { get; } = new();
        public HashSet<int> OccupiedBinIds { get; } = new();
        public HashSet<string> BorrowDocumentNos { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RepairDocumentNos { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<(ExternalPartyType PartyType, string PartyCode)> ActivePartyKeys { get; } = new();
        public HashSet<(int WarehouseId, int ItemId, string SnCode)> QuantityBalanceKeys { get; } = new();
    }
    public async Task<ServiceResult<int>> UploadAsync(string importType, string fileName, Stream fileStream, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        importType = NormalizeImportType(importType);
        if (!ImportHeaders.ContainsKey(importType))
        {
            return ServiceResult<int>.Fail("Import type is not supported.");
        }

        if (!CanUseImportType(importType, user))
        {
            return ServiceResult<int>.Fail("Current role cannot use this import type.");
        }

        var orderedHeaders = ImportHeaders[importType];
        using var uploaded = new MemoryStream();
        await fileStream.CopyToAsync(uploaded, cancellationToken);
        var originalBytes = uploaded.ToArray();
        uploaded.Position = 0;

        var rows = (await SimpleExcel.ReadTableByOrderAsync(uploaded, fileName, orderedHeaders, cancellationToken))
            .Select(row => CanonicalizeImportRow(importType, row))
            .Where(x => !IsInstructionRow(x))
            .ToArray();
        if (rows.Length == 0)
        {
            return ServiceResult<int>.Fail("File does not contain data rows.");
        }

        var shouldStoreOriginal = originalBytes.Length <= MaxStoredOriginalFileBytes;
        var batch = new ImportBatch
        {
            BatchNo = _documentNumbers.Next("IMP", DateTime.UtcNow),
            ImportType = importType,
            FileName = fileName,
            OriginalFileContent = shouldStoreOriginal ? originalBytes : null,
            OriginalContentType = shouldStoreOriginal ? ContentTypeForFile(fileName) : null,
            Status = ImportBatchStatus.Uploaded,
            TotalRows = rows.Length,
            CreatedBy = user.UserName
        };

        var rowNumber = 2;
        foreach (var row in rows)
        {
            batch.Rows.Add(new ImportBatchRow
            {
                RowNumber = rowNumber++,
                RawJson = JsonSerializer.Serialize(row, JsonOptions),
                Severity = ValidationSeverity.Info,
                IsValid = true,
                CreatedBy = user.UserName
            });
        }

        var autoDetectChanges = _db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = false;
            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

        return ServiceResult<int>.Ok(batch.Id, "File uploaded.");
    }

    public async Task<ServiceResult<int>> ValidateAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var batch = await _db.ImportBatches.Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);
        if (batch == null)
        {
            return ServiceResult<int>.Fail("Import batch not found.");
        }

        if (!CanUseImportType(batch.ImportType, user))
        {
            return ServiceResult<int>.Fail("Current role cannot use this import type.");
        }

        if (batch.ImportType.Equals("ItemMasterUpdate", StringComparison.OrdinalIgnoreCase))
        {
            return await ValidateItemMasterUpdateBatchAsync(batch, user, cancellationToken);
        }

        var blocking = 0;
        var dataRows = batch.Rows.OrderBy(x => x.RowNumber).Select(x => new { Row = x, Data = Row(x) }).ToArray();
        var batchErrors = BuildBatchValidationErrors(batch.ImportType, dataRows.Select(x => (x.Row, x.Data)).ToArray());
        var validationContext = await BuildImportValidationContextAsync(batch.ImportType, dataRows.Select(x => x.Data).ToArray(), cancellationToken);
        foreach (var rowData in dataRows)
        {
            var row = rowData.Row;
            var errors = ValidateRow(batch.ImportType, rowData.Data, validationContext, user);
            if (batchErrors.TryGetValue(row.Id, out var extraErrors))
            {
                errors.AddRange(extraErrors);
            }

            row.IsValid = errors.Count == 0;
            row.Severity = errors.Count == 0 ? ValidationSeverity.Info : ValidationSeverity.Blocking;
            row.Message = errors.Count == 0 ? "OK" : string.Join("; ", errors);
            row.SuggestedFix = errors.Count == 0 ? null : "Correct the row and upload again.";
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserName;
            if (errors.Count > 0)
            {
                blocking++;
            }
        }

        batch.BlockingErrorRows = blocking;
        batch.Status = blocking == 0 ? ImportBatchStatus.Validated : ImportBatchStatus.Blocked;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = user.UserName;
        var autoDetectChanges = _db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
        if(blocking > 0) return ServiceResult<int>.Fail("Import batch has blocking errors.");
        return ServiceResult<int>.Ok(blocking, "Import batch is valid.");
    }

    public async Task<ServiceResult<int>> ConfirmAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var batch = await _db.ImportBatches.Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);
        if (batch == null)
        {
            return ServiceResult<int>.Fail("Import batch not found.");
        }

        if (!CanUseImportType(batch.ImportType, user))
        {
            return ServiceResult<int>.Fail("Current role cannot use this import type.");
        }

        if (!HasFreshValidation(batch))
        {
            var validate = await ValidateAsync(importBatchId, user, cancellationToken);
            if (!validate.Success)
            {
                return validate;
            }

            batch = await _db.ImportBatches.Include(x => x.Rows).FirstAsync(x => x.Id == importBatchId, cancellationToken);
        }

        if (batch.BlockingErrorRows > 0)
        {
            return ServiceResult<int>.Fail("Import batch has blocking errors.");
        }

        var rows = batch.Rows.OrderBy(x => x.RowNumber).Select(Row).ToArray();
        var useOuterTransaction = RequiresOuterImportTransaction(batch.ImportType);
        await using var tx = useOuterTransaction ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        var inserted = await ConfirmRowsAsync(batch.ImportType, rows, user, cancellationToken);

        batch.Status = ImportBatchStatus.Confirmed;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = user.UserName;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = "ConfirmImport",
            EntityName = nameof(ImportBatch),
            EntityId = batch.Id,
            ReferenceNo = batch.BatchNo,
            Result = "Success",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        if (tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }
        return ServiceResult<int>.Ok(inserted, "Import confirmed.");
    }

    private static bool HasFreshValidation(ImportBatch batch)
    {
        if (batch.Status != ImportBatchStatus.Validated || batch.BlockingErrorRows != 0 || !batch.UpdatedAt.HasValue)
        {
            return false;
        }

        if (batch.Rows.Count != batch.TotalRows)
        {
            return false;
        }

        var validatedAt = batch.UpdatedAt.Value;
        return batch.Rows.All(row =>
            row.IsValid &&
            row.Severity == ValidationSeverity.Info &&
            row.Message == "OK" &&
            row.SuggestedFix == null &&
            row.UpdatedAt.HasValue &&
            row.UpdatedAt.Value <= validatedAt &&
            row.CreatedAt <= validatedAt);
    }

    private async Task<int> ConfirmRowsAsync(string importType, IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        return importType switch
        {
            "ItemMaster"         => await ConfirmItemMasterAsync(rows, user, cancellationToken),
            "ItemMasterUpdate"   => await ConfirmItemMasterUpdateAsync(rows, user, cancellationToken),
            "WarehouseStructure" => await ConfirmWarehouseStructureAsync(rows, user, cancellationToken),
            "Inbound"            => await ConfirmInboundAsync(rows, user, cancellationToken),
            "InventoryCheck"     => await ConfirmInventoryCheckAsync(rows, user, cancellationToken),
            "RepairSend"         => await ConfirmRepairSendAsync(rows, user, cancellationToken),
            "BorrowLend"         => await ConfirmBorrowLendAsync(rows, user, cancellationToken),
            "QuantityInbound"    => await ConfirmQuantityOperationImportAsync("Receive", rows, user, cancellationToken),
            "QuantityOutbound"   => await ConfirmQuantityOperationImportAsync("Issue", rows, user, cancellationToken),
            "QuantityAdjust"     => await ConfirmQuantityOperationImportAsync("Adjust", rows, user, cancellationToken),
            "MoveLocation"       => await ConfirmMoveLocationImportAsync(rows, user, cancellationToken),
            "BorrowReturn"       => await ConfirmBorrowReturnImportAsync(rows, user, cancellationToken),
            "RepairReceive"      => await ConfirmRepairReceiveImportAsync(rows, user, cancellationToken),
            _                    => throw new InvalidOperationException("Unsupported import type.")
        };
    }

    private static bool RequiresOuterImportTransaction(string importType)
    {
        return importType is "ItemMaster" or "ItemMasterUpdate" or "WarehouseStructure" or "Inbound" or "RepairSend" or "BorrowLend";
    }

    public async Task<ServiceResult<IReadOnlyCollection<ImportBatchDto>>> ListAsync(CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var rows = await _db.ImportBatches.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new ImportBatchDto
            {
                Id = x.Id,
                BatchNo = x.BatchNo,
                ImportType = x.ImportType,
                FileName = x.FileName,
                Status = x.Status.ToString(),
                TotalRows = x.TotalRows,
                BlockingErrorRows = x.BlockingErrorRows,
                HasOriginalFile = x.OriginalFileContent != null
            })
            .ToArrayAsync(cancellationToken);
        return ServiceResult<IReadOnlyCollection<ImportBatchDto>>.Ok(rows);
    }

    public async Task<ServiceResult<IReadOnlyCollection<ImportValidationRowDto>>> RowsAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var rows = await _db.ImportBatchRows.AsNoTracking()
            .Where(x => x.ImportBatchId == importBatchId)
            .OrderBy(x => x.RowNumber)
            .Select(x => new ImportValidationRowDto
            {
                Id = x.Id,
                RowNumber = x.RowNumber,
                ColumnName = x.ColumnName,
                Severity = x.Severity.ToString(),
                Message = x.Message,
                SuggestedFix = x.SuggestedFix,
                IsValid = x.IsValid
            })
            .ToArrayAsync(cancellationToken);
        return ServiceResult<IReadOnlyCollection<ImportValidationRowDto>>.Ok(rows);
    }

    public Task<byte[]> TemplateAsync(string importType, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        importType = NormalizeImportType(importType);
        var headers = ImportHeaders.TryGetValue(importType, out var value) ? value : ImportHeaders["Inbound"];
        if (!CanUseImportType(importType, user))
        {
            headers = ImportHeaders["Inbound"];
        }

        var localizedHeaders = headers.Select(header => ExcelText(user, header)).ToArray();
        var bytes = SimpleExcel.CreateWorkbook(localizedHeaders, TemplateRows(importType), $"{importType}Template");
        return Task.FromResult(bytes);
    }

    public async Task<ServiceResult<ImportBatchFileDto>> DownloadAsync(int importBatchId, string kind, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedKind = NormalizeDownloadKind(kind);
        if (normalizedKind == "original")
        {
            var original = await _db.ImportBatches
                .AsNoTracking()
                .Where(x => x.Id == importBatchId)
                .Select(x => new { x.ImportType, x.FileName, x.OriginalContentType, x.OriginalFileContent })
                .FirstOrDefaultAsync(cancellationToken);
            if (original == null)
            {
                return ServiceResult<ImportBatchFileDto>.Fail("Import batch not found.");
            }

            if (!CanUseImportType(original.ImportType, user))
            {
                return ServiceResult<ImportBatchFileDto>.Fail("Current role cannot use this import type.");
            }

            if (original.OriginalFileContent == null || original.OriginalFileContent.Length == 0)
            {
                return ServiceResult<ImportBatchFileDto>.Fail("Original uploaded file is not available.");
            }

            return ServiceResult<ImportBatchFileDto>.Ok(new ImportBatchFileDto
            {
                FileName = SafeDownloadName(original.ImportType, "Original", Path.GetExtension(original.FileName)),
                ContentType = original.OriginalContentType ?? ContentTypeForFile(original.FileName),
                Content = original.OriginalFileContent
            });
        }

        var batch = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => x.Id == importBatchId)
            .Select(x => new { x.Id, x.ImportType })
            .FirstOrDefaultAsync(cancellationToken);
        if (batch == null)
        {
            return ServiceResult<ImportBatchFileDto>.Fail("Import batch not found.");
        }

        if (!CanUseImportType(batch.ImportType, user))
        {
            return ServiceResult<ImportBatchFileDto>.Fail("Current role cannot use this import type.");
        }

        if (!ImportHeaders.TryGetValue(batch.ImportType, out var headers))
        {
            return ServiceResult<ImportBatchFileDto>.Fail("Import type is not supported.");
        }

        if (normalizedKind == "template")
        {
            var template = await TemplateAsync(batch.ImportType, user, cancellationToken);
            return ServiceResult<ImportBatchFileDto>.Ok(new ImportBatchFileDto
            {
                FileName = SafeDownloadName(batch.ImportType, "Template"),
                Content = template
            });
        }

        var rowsQuery = _db.ImportBatchRows
            .AsNoTracking()
            .Where(x => x.ImportBatchId == importBatchId);
        if (normalizedKind == "validation-errors")
        {
            rowsQuery = rowsQuery.Where(x => !x.IsValid || x.Severity == ValidationSeverity.Blocking);
        }

        var rows = await rowsQuery
            .OrderBy(x => x.RowNumber)
            .ToArrayAsync(cancellationToken);

        var exportRows = rows
            .Select(row => OrderedBatchExportRow(batch.ImportType, row, headers))
            .ToArray();

        var exportHeaders = Headers(user, headers.Concat(new[] { "Row", "Severity", "Message", "Suggested Fix" }).ToArray());
        var label = normalizedKind switch
        {
            "validation-errors" => "ValidationErrors",
            "result" => "Result",
            _ => "Preview"
        };
        var bytes = SimpleExcel.CreateWorkbook(exportHeaders, exportRows, $"{batch.ImportType}{label}");
        return ServiceResult<ImportBatchFileDto>.Ok(new ImportBatchFileDto
        {
            FileName = SafeDownloadName(batch.ImportType, label),
            Content = bytes
        });
    }

    public async Task<byte[]> ExportInventoryAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        ItemStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<ItemStatus>(filter.Status, true, out var parsed))
        {
            status = parsed;
        }

        var query = _db.CurrentItemLocations.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Warehouse)
            .Include(x => x.BinLocation)
            .Include(x => x.ExternalParty)
            .Where(x => !x.IsDeleted && x.ItemInstance != null && !x.ItemInstance.IsDeleted)
            .AsQueryable();

        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null && x.ItemInstance.ItemId == filter.CategoryId.Value);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        if (status.HasValue)
        {
            if (status == ItemStatus.InStock) query = query.Where(x => x.ItemInstance != null && (x.ItemInstance.Status == ItemStatus.InStock || x.ItemInstance.Status == ItemStatus.Normal || x.ItemInstance.Status == ItemStatus.Scrapped || x.ItemInstance.Status == ItemStatus.Damaged));
            else query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Status == status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(x => x.UpdatedLocationAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.UpdatedLocationAt < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var key = filter.Keyword.Trim();
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null &&
                (x.ItemInstance.Item.ItemCode.Contains(key) ||
                 (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                 (x.BinLocation != null && x.BinLocation.BinCode.Contains(key))));
        }

        var rows = await query.OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Take(10000)
            .Select(x => new object?[]
            {
                x.ItemInstance!.Item!.ItemCode,
                x.ItemInstance.Item.DefaultName,
                x.ItemInstance.SerialNumber,
                x.ItemInstance.MT,
                ExcelText(user, $"Enum.ItemStatus.{(x.ItemInstance.Status == ItemStatus.InStock? ItemStatus.Normal : x.ItemInstance.Status)}"),
                x.Warehouse != null ? x.Warehouse.WarehouseCode : string.Empty,
                x.BinLocation != null ? x.BinLocation.BinCode : string.Empty,
                x.ExternalParty != null ? x.ExternalParty.Name : string.Empty,
                x.ReferenceDocumentNo,
                x.UpdatedLocationAt,
                x.UpdatedLocationBy
            })
            .ToArrayAsync(cancellationToken);

        return SimpleExcel.CreateWorkbook(Headers(user, "ItemCode", "ItemName", "SerialNumber", "MT", "Status", "Warehouse", "Location", "Holder", "ReferenceDocumentNo", "UpdatedAt", "UpdatedBy"), rows, ExcelText(user, "Inventory"));
    }

    public async Task<byte[]> ExportHistoryAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.ItemMovementHistories.AsNoTracking()
            .Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Where(x => x.ItemInstance != null && !x.ItemInstance.IsDeleted)
            .AsQueryable();

        ItemStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<ItemStatus>(filter.Status, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        if (filter.ItemInstanceId.HasValue)
        {
            query = query.Where(x => x.ItemInstanceId == filter.ItemInstanceId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(x => x.PerformedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.PerformedAt < to);
        }

        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && !x.ItemInstance.IsDeleted && _db.CurrentItemLocations.Any(c => c.ItemInstanceId == x.ItemInstanceId && !c.IsDeleted && c.WarehouseId == filter.WarehouseId.Value));
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null && x.ItemInstance.Item.CategoryId == filter.CategoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.NewStatus == status.Value || x.OldStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var key = filter.Keyword.Trim();
            query = query.Where(x =>
                x.DocumentNo.Contains(key) ||
                x.PerformedBy.Contains(key) ||
                (x.Note != null && x.Note.Contains(key)) ||
                (x.ItemInstance != null && x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                (x.ItemInstance != null && x.ItemInstance.Item != null && (x.ItemInstance.Item.ItemCode.Contains(key) || x.ItemInstance.Item.DefaultName.Contains(key))));
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.ItemInstance != null && !x.ItemInstance.IsDeleted && _db.CurrentItemLocations.Any(c =>
                c.ItemInstanceId == x.ItemInstanceId &&
                !c.IsDeleted &&
                (c.WarehouseId == null || user.WarehouseIds.Contains(c.WarehouseId.Value))));
        }

        var rows = await query.OrderByDescending(x => x.PerformedAt)
            .Take(10000)
            .Select(x => new object?[]
            {
                x.PerformedAt,
                x.ItemInstance != null && x.ItemInstance.Item != null ? x.ItemInstance.Item.ItemCode : string.Empty,
                x.ItemInstance != null ? x.ItemInstance.SerialNumber : string.Empty,
                ExcelText(user, $"Enum.MovementActionType.{x.ActionType}"),
                x.FromLocationDisplay,
                x.ToLocationDisplay,
                ExcelText(user, $"Enum.ItemStatus.{x.OldStatus}"),
                ExcelText(user, $"Enum.ItemStatus.{x.NewStatus}"),
                x.DocumentNo,
                x.PerformedBy,
                x.Note
            })
            .ToArrayAsync(cancellationToken);

        return SimpleExcel.CreateWorkbook(Headers(user, "PerformedAt", "ItemCode", "SerialNumber", "Action", "FromLocation", "ToLocation", "OldStatus", "NewStatus", "DocumentNo", "PerformedBy", "Note"), rows, ExcelText(user, "History"));
    }

    public async Task<byte[]> ExportAuditAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (filter.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var key = filter.Keyword.Trim();
            query = query.Where(x => x.UserName.Contains(key) || x.Action.Contains(key) || x.EntityName.Contains(key) || (x.ReferenceNo != null && x.ReferenceNo.Contains(key)));
        }

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            var userName = filter.UserName.Trim();
            query = query.Where(x => x.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(x => x.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entityName = filter.EntityName.Trim();
            query = query.Where(x => x.EntityName.Contains(entityName));
        }

        if (!string.IsNullOrWhiteSpace(filter.ReferenceNo))
        {
            var referenceNo = filter.ReferenceNo.Trim();
            query = query.Where(x => x.ReferenceNo != null && x.ReferenceNo.Contains(referenceNo));
        }

        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Take(10000)
            .Select(x => new object?[] { x.CreatedAt, x.UserName, ExcelText(user, $"AuditAction.{x.Action}"), ExcelText(user, $"AuditEntity.{x.EntityName}"), x.ReferenceNo, ExcelText(user, x.Result) })
            .ToArrayAsync(cancellationToken);

        return SimpleExcel.CreateWorkbook(Headers(user, "CreatedAt", "UserName", "Action", "EntityName", "ReferenceNo", "Result"), rows, ExcelText(user, "Audit"));
    }

    // ─── Phase 6: New Export Methods ───────────────────────────────────────

    public async Task<byte[]> ExportQuantityBalanceAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.QuantityStockBalances.Include(x => x.Item).ThenInclude(x => x!.Category).Include(x => x.Warehouse).AsQueryable();
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        var balances = await query.Where(x => x.Quantity > 0)
            .OrderBy(x => x.Warehouse!.WarehouseCode).ThenBy(x => x.Item!.ItemCode).ThenBy(x => x.SnCode)
            .Take(50000)
            .ToArrayAsync(cancellationToken);
        var ownerNames = await LoadQuantityOwnerNamesAsync(
            balances.Select(x => (x.ItemId, x.SnCode)),
            cancellationToken);
        var rows = balances.Select(x => new object?[]
        {
            string.Empty,
            string.Empty,
            x.Warehouse!.WarehouseCode,
            x.Item!.Category != null ? x.Item.Category.CategoryCode : string.Empty,
            x.Item.ItemCode,
            x.SnCode,
            x.Quantity,
            x.Status.ToString(),
            QuantityOwnerName(ownerNames, x.ItemId, x.SnCode),
            string.Empty
        }).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "QuantityInbound"), rows, ExcelText(user, "QuantityBalance"));
    }

    public async Task<byte[]> ExportInboundDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.InboundDocuments
            .Include(x => x.Lines).ThenInclude(l => l.ItemInstance).ThenInclude(i => i!.Item)
            .Include(x => x.Lines).ThenInclude(l => l.BinLocation)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceExternalParty)
            .Include(x => x.Receiver)
            .AsQueryable();
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.Lines.Any(l => l.BinLocation != null && l.BinLocation.WarehouseId == filter.WarehouseId.Value));
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var rows = docs.SelectMany(d => d.Lines.Select(l => new object?[]
        {
            d.DocumentDate.ToString("yyyy-MM-dd"), d.DocumentNo, l.ItemInstance?.Item?.ItemCode, l.ItemInstance?.SerialNumber, l.ItemInstance?.Barcode, l.ItemInstance?.MT,
            d.Warehouse?.WarehouseCode, l.BinLocation?.BinCode, d.SourceExternalParty?.PartyCode, l.ItemInstance?.Status.ToString(), l.Note,
            d.Receiver?.PartyCode, d.Receiver?.Name, d.PartyPhone, d.PartyDepartment, l.ItemInstance?.OwnerName, l.ItemInstance?.TrackingType.ToString()
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "Inbound"), rows, ExcelText(user, "InboundDocuments"));
    }

    public async Task<byte[]> ExportBorrowDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.BorrowDocuments
            .Include(x => x.Lines).ThenInclude(l => l.ItemInstance).ThenInclude(i => i!.Item)
            .Include(x => x.Lines).ThenInclude(l => l.FromBinLocation).ThenInclude(b => b!.Warehouse)
            .Include(x => x.Borrower)
            .AsQueryable();
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var rows = docs.SelectMany(d => d.Lines
            .Where(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted)
            .Select(l => new object?[]
        {
            d.Borrower?.PartyCode, l.FromBinLocation?.Warehouse?.WarehouseCode, d.DocumentNo, d.DocumentDate.ToString("yyyy-MM-dd"), d.DueDate.ToString("yyyy-MM-dd"),
            d.Purpose, d.BorrowDepartment, d.BorrowerPhone, d.DepartmentOwner,
            l.ItemInstance?.Item?.ItemCode, l.ItemInstance?.SerialNumber, l.TargetExternalLocation
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "BorrowLend"), rows, ExcelText(user, "BorrowDocuments"));
    }

    public async Task<byte[]> ExportRepairDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.RepairDocuments
            .Include(x => x.Lines).ThenInclude(l => l.ItemInstance)
            .Include(x => x.RepairVendor)
            .AsQueryable();
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var rows = docs.SelectMany(d => d.Lines
            .Where(l => l.ItemInstance == null || !l.ItemInstance.IsDeleted)
            .Select(l => new object?[]
        {
            d.DocumentNo, d.RepairVendor?.PartyCode, d.RepairVendor?.Name, l.ItemInstance?.SerialNumber, l.ItemInstance?.Barcode,
            d.Reason, d.ExpectedReturnDate?.ToString("yyyy-MM-dd"), l.TargetExternalLocation
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "RepairSend"), rows, ExcelText(user, "RepairDocuments"));
    }

    public async Task<byte[]> ExportMoveDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.MoveDocuments
            .Include(x => x.Lines).ThenInclude(l => l.ItemInstance)
            .Include(x => x.Lines).ThenInclude(l => l.TargetBinLocation).ThenInclude(b => b!.Warehouse)
            .AsQueryable();
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var rows = docs.SelectMany(d => d.Lines.Select(l => new object?[]
        {
            d.DocumentDate.ToString("yyyy-MM-dd"), l.ItemInstance?.SerialNumber, l.ItemInstance?.Barcode,
            string.Empty, l.TargetBinLocation?.Warehouse?.WarehouseCode, l.TargetBinLocation?.BinCode, d.Note
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "MoveLocation"), rows, ExcelText(user, "MoveDocuments"));
    }

    public async Task<byte[]> ExportAdjustmentDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        // Use QuantityInventoryDocuments for quantity-type adjustments
        var query = _db.QuantityInventoryDocuments
            .Include(x => x.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Category)
            .Include(x => x.Warehouse)
            .Where(x => x.DocumentType == QuantityInventoryDocumentType.Adjust)
            .AsQueryable();
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var ownerNames = await LoadQuantityOwnerNamesAsync(
            docs.SelectMany(d => d.Lines).Select(l => (l.ItemId, l.SnCode)),
            cancellationToken);
        var rows = docs.SelectMany(d => d.Lines.Select(l => new object?[]
        {
            d.DocumentNo, d.DocumentDate.ToString("yyyy-MM-dd"), d.Warehouse?.WarehouseCode, l.Item?.Category?.CategoryCode,
            l.Item?.ItemCode, l.SnCode, l.Quantity, l.Status.ToString(),
            QuantityOwnerName(ownerNames, l.ItemId, l.SnCode),
            l.Note
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "QuantityAdjust"), rows, ExcelText(user, "AdjustmentDocuments"));
    }

    public async Task<byte[]> ExportInventoryCheckDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryCheckDocuments
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(l => l.ItemInstance).ThenInclude(i => i!.Item)
            .AsQueryable();
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        if (filter.FromDate.HasValue) query = query.Where(x => x.DocumentDate >= filter.FromDate.Value);
        var docs = await query.OrderByDescending(x => x.DocumentDate).Take(5000).ToListAsync(cancellationToken);
        var actualBinIds = docs.SelectMany(d => d.Lines).Select(l => l.ActualBinLocationId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var actualBins = await _db.BinLocations.AsNoTracking().Where(x => actualBinIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var rows = docs.SelectMany(d => d.Lines.Select(l => new object?[]
        {
            d.Warehouse?.WarehouseCode, l.ItemInstance?.Item?.ItemCode, l.ItemInstance?.SerialNumber,
            l.ActualBinLocationId.HasValue && actualBins.TryGetValue(l.ActualBinLocationId.Value, out var bin) ? bin.BinCode : string.Empty,
            l.Note
        })).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "InventoryCheck"), rows, ExcelText(user, "InventoryCheckDocuments"));
    }

    public async Task<byte[]> ExportQuantityTransactionsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.QuantityInventoryTransactions.Include(x => x.Warehouse).Include(x => x.Item).ThenInclude(i => i!.Category).AsQueryable();
        if (filter.WarehouseId.HasValue) query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        if (filter.FromDate.HasValue) query = query.Where(x => x.PostedAt >= filter.FromDate.Value);
        var rows = await query.OrderByDescending(x => x.PostedAt).Take(50000).ToListAsync(cancellationToken);
        var ownerNames = await LoadQuantityOwnerNamesAsync(
            rows.Select(x => (x.ItemId, x.SnCode)),
            cancellationToken);
        var data = rows.Select(x => new object?[] {
            x.DocumentNo,
            x.PostedAt.ToString("yyyy-MM-dd"),
            x.Warehouse?.WarehouseCode ?? x.WarehouseId.ToString(),
            x.Item?.Category?.CategoryCode ?? string.Empty,
            x.Item?.ItemCode ?? string.Empty,
            x.SnCode,
            Math.Abs(x.QuantityDelta),
            x.StatusAfter.ToString(),
            QuantityOwnerName(ownerNames, x.ItemId, x.SnCode),
            string.Empty
        }).ToArray();
        return SimpleExcel.CreateWorkbook(ImportTemplateHeaders(user, "QuantityInbound"), data, ExcelText(user, "QuantityTransactions"));
    }

    public async Task<byte[]> ExportItemMasterAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var items = await _db.Items.Include(x => x.Category).Include(x => x.Unit).Include(x => x.Translations)
            .Where(x => x.IsActive).OrderBy(x => x.ItemCode).Take(50000).ToListAsync(cancellationToken);
        var rows = items.Select(i =>
        {
            var vi = i.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Value ?? string.Empty;
            var en = i.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Value ?? string.Empty;
            var zh = i.Translations.FirstOrDefault(t => t.LanguageCode == "zh")?.Value ?? string.Empty;
            return new object?[] { i.ItemCode, i.DefaultName, i.Category?.CategoryCode, i.Category?.Name, i.Unit?.UnitCode, i.Unit?.Name, i.IsSerialManaged ? "yes" : "no", vi, en, zh };
        }).ToArray();
        return SimpleExcel.CreateWorkbook(Headers(user, "ItemCode", "DefaultName", "CategoryCode", "CategoryName", "UnitCode", "UnitName", "IsSerialManaged", "NameVi", "NameEn", "NameZh"), rows, ExcelText(user, "ItemMaster"));
    }

    public async Task<byte[]> ExportWarehouseStructureAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var bins = await _db.BinLocations
            .Include(x => x.Shelf).ThenInclude(s => s!.Rack).ThenInclude(r => r!.WarehouseZone).ThenInclude(z => z!.Warehouse).ThenInclude(w => w!.Branch).ThenInclude(b => b!.Company)
            .Where(x => x.IsActive)
            .OrderBy(x => x.BinCode).Take(50000).ToListAsync(cancellationToken);
        var rows = bins.Select(b =>
        {
            var shelf = b.Shelf; var rack = shelf?.Rack; var zone = rack?.WarehouseZone; var wh = zone?.Warehouse; var branch = wh?.Branch; var company = branch?.Company;
            return new object?[] { company?.Code, company?.Name, branch?.Code, branch?.Name, wh?.WarehouseCode, wh?.Name, zone?.ZoneCode, zone?.Name, rack?.RackCode, rack?.Name, shelf?.ShelfCode, shelf?.Name, b.BinCode, b.UsageType.ToString() };
        }).ToArray();
        return SimpleExcel.CreateWorkbook(Headers(user, "CompanyCode", "CompanyName", "BranchCode", "BranchName", "WarehouseCode", "WarehouseName", "ZoneCode", "ZoneName", "RackCode", "RackName", "ShelfCode", "ShelfName", "BinCode", "UsageType"), rows, ExcelText(user, "WarehouseStructure"));
    }

    private async Task<ImportValidationContext> BuildImportValidationContextAsync(string importType, IReadOnlyCollection<Dictionary<string, string>> rows, CancellationToken cancellationToken)
    {
        var context = new ImportValidationContext();

        var itemCodes = DistinctCodes(rows, "ItemCode");
        foreach (var chunk in Batch(itemCodes))
        {
            var items = await _db.Items.AsNoTracking()
                .Where(x => chunk.Contains(x.ItemCode))
                .ToListAsync(cancellationToken);
            foreach (var item in items)
            {
                var key = NormalizeCode(item.ItemCode);
                context.ExistingItemCodes.Add(key);
                if (item.IsActive && !context.ActiveItemsByCode.ContainsKey(key))
                {
                    context.ActiveItemsByCode[key] = item;
                }
            }
        }

        var warehouseCodes = DistinctCodes(rows, "WarehouseCode", "TargetWarehouseCode");
        foreach (var chunk in Batch(warehouseCodes))
        {
            var warehouses = await _db.Warehouses.AsNoTracking()
                .Where(x => x.IsActive && chunk.Contains(x.WarehouseCode))
                .ToListAsync(cancellationToken);
            foreach (var warehouse in warehouses)
            {
                context.ActiveWarehousesByCode.TryAdd(NormalizeCode(warehouse.WarehouseCode), warehouse);
            }
        }

        var warehouseIds = context.ActiveWarehousesByCode.Values.Select(x => x.Id).ToHashSet();
        var binCodes = DistinctCodes(rows, "BinCode", "ActualBinCode", "TargetBinCode", "ReturnLocationBinCode");
        if (warehouseIds.Count > 0 && binCodes.Length > 0)
        {
            foreach (var chunk in Batch(binCodes))
            {
                var bins = await _db.BinLocations.AsNoTracking()
                    .Where(x => chunk.Contains(x.BinCode))
                    .ToListAsync(cancellationToken);
                foreach (var bin in bins)
                {
                    if (!warehouseIds.Contains(bin.WarehouseId))
                    {
                        continue;
                    }

                    var key = (bin.WarehouseId, NormalizeCode(bin.BinCode));
                    context.ExistingBinKeys.Add(key);
                    if (bin.IsActive && !context.ActiveBinsByWarehouseAndCode.ContainsKey(key))
                    {
                        context.ActiveBinsByWarehouseAndCode[key] = bin;
                    }
                }
            }
        }

        var serials = DistinctCodes(rows, "SerialNumber", "Barcode");
        var itemCodeSet = itemCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (serials.Length > 0)
        {
            foreach (var chunk in Batch(serials))
            {
                var instances = await _db.ItemInstances.AsNoTracking()
                    .Include(x => x.Item)
                    .Where(x =>
                        ((x.SerialNumber != null && chunk.Contains(x.SerialNumber)) ||
                         (x.Barcode != null && chunk.Contains(x.Barcode))))
                    .ToListAsync(cancellationToken);
                foreach (var instance in instances)
                {
                    if (instance.IsDeleted)
                    {
                        if (instance.Item != null && !string.IsNullOrWhiteSpace(instance.SerialNumber))
                        {
                            context.DeletedInstanceKeys.Add((NormalizeCode(instance.Item.ItemCode), NormalizeCode(instance.SerialNumber)));
                        }

                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(instance.SerialNumber))
                    {
                        context.InstancesBySerialOrBarcode.TryAdd(NormalizeCode(instance.SerialNumber), instance);
                    }
                    if (!string.IsNullOrWhiteSpace(instance.Barcode))
                    {
                        context.InstancesBySerialOrBarcode.TryAdd(NormalizeCode(instance.Barcode), instance);
                    }

                    if (instance.Item == null)
                    {
                        continue;
                    }

                    var itemCode = NormalizeCode(instance.Item.ItemCode);
                    if (itemCodeSet.Count > 0 && !itemCodeSet.Contains(itemCode))
                    {
                        continue;
                    }

                    var key = (itemCode, NormalizeCode(instance.SerialNumber));
                    context.InstancesByItemAndSerial.TryAdd(key, instance);
                }
            }
        }

        var instanceIds = context.InstancesByItemAndSerial.Values.Select(x => x.Id).Distinct().ToArray();
        foreach (var chunk in Batch(instanceIds))
        {
            var locations = await _db.CurrentItemLocations.AsNoTracking()
                .Where(x => chunk.Contains(x.ItemInstanceId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var location in locations)
            {
                context.CurrentLocationsByInstanceId.TryAdd(location.ItemInstanceId, location);
            }
        }

        var binIds = importType == "InventoryCheck"
            ? Array.Empty<int>()
            : context.ActiveBinsByWarehouseAndCode.Values.Select(x => x.Id).Distinct().ToArray();
        foreach (var chunk in Batch(binIds))
        {
            var occupiedBinIds = await _db.CurrentItemLocations.AsNoTracking()
                .Where(x =>
                    x.BinLocationId.HasValue &&
                    !x.IsDeleted &&
                    chunk.Contains(x.BinLocationId.Value) &&
                    x.ItemInstance != null &&
                    x.ItemInstance.IsActive &&
                    !x.ItemInstance.IsDeleted &&
                    x.ItemInstance.Status != ItemStatus.Lost &&
                    x.ItemInstance.Status != ItemStatus.Disposed)
                .Select(x => x.BinLocationId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var binId in occupiedBinIds)
            {
                context.OccupiedBinIds.Add(binId);
            }
        }

        var borrowDocumentNos = DistinctCodes(rows, "BorrowDocumentNo");
        foreach (var chunk in Batch(borrowDocumentNos))
        {
            var documentNos = await _db.BorrowDocuments.AsNoTracking()
                .Where(x => chunk.Contains(x.DocumentNo))
                .Select(x => x.DocumentNo)
                .ToListAsync(cancellationToken);
            foreach (var documentNo in documentNos)
            {
                context.BorrowDocumentNos.Add(NormalizeCode(documentNo));
            }
        }

        var repairDocumentNos = DistinctCodes(rows, "RepairDocumentNo");
        foreach (var chunk in Batch(repairDocumentNos))
        {
            var documentNos = await _db.RepairDocuments.AsNoTracking()
                .Where(x => chunk.Contains(x.DocumentNo))
                .Select(x => x.DocumentNo)
                .ToListAsync(cancellationToken);
            foreach (var documentNo in documentNos)
            {
                context.RepairDocumentNos.Add(NormalizeCode(documentNo));
            }
        }

        var repairVendorCodes = DistinctCodes(rows, "RepairVendorCode");
        foreach (var chunk in Batch(repairVendorCodes))
        {
            var partyCodes = await _db.ExternalParties.AsNoTracking()
                .Where(x => x.PartyType == ExternalPartyType.RepairVendor && x.IsActive && chunk.Contains(x.PartyCode))
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            foreach (var partyCode in partyCodes)
            {
                context.ActivePartyKeys.Add((ExternalPartyType.RepairVendor, NormalizeCode(partyCode)));
            }
        }

        if (importType is "QuantityOutbound" or "QuantityAdjust")
        {
            var balanceCandidates = rows
                .Select(row =>
                {
                    context.ActiveWarehousesByCode.TryGetValue(NormalizeCode(Value(row, "WarehouseCode")), out var warehouse);
                    context.ActiveItemsByCode.TryGetValue(NormalizeCode(Value(row, "ItemCode")), out var item);
                    return new
                    {
                        Warehouse = warehouse,
                        Item = item,
                        SnCode = NormalizeCode(Value(row, "SnCode"))
                    };
                })
                .Where(x => x.Warehouse != null && x.Item != null && !string.IsNullOrWhiteSpace(x.SnCode))
                .Select(x => (x.Warehouse!.Id, x.Item!.Id, x.SnCode))
                .Distinct()
                .ToArray();

            var candidateSet = balanceCandidates.ToHashSet();
            foreach (var chunk in Batch(balanceCandidates.Select(x => x.SnCode).Distinct(StringComparer.OrdinalIgnoreCase)))
            {
                var balances = await _db.QuantityStockBalances.AsNoTracking()
                    .Where(x => chunk.Contains(x.SnCode))
                    .Select(x => new { x.WarehouseId, x.ItemId, x.SnCode })
                    .ToListAsync(cancellationToken);
                foreach (var balance in balances)
                {
                    var key = (balance.WarehouseId, balance.ItemId, NormalizeCode(balance.SnCode));
                    if (candidateSet.Contains(key))
                    {
                        context.QuantityBalanceKeys.Add(key);
                    }
                }
            }
        }

        return context;
    }

    private static List<string> ValidateRow(string importType, Dictionary<string, string> row, ImportValidationContext context, CurrentUserContext user)
    {
        var errors = RequiredColumns(importType).Where(x => string.IsNullOrWhiteSpace(Value(row, x))).Select(x => $"{x} is required.").ToList();
        if (errors.Count > 0)
        {
            return errors;
        }

        switch (importType)
        {
            case "Inbound":
                ValidateInboundRow(row, errors, context, user);
                break;
            case "InventoryCheck":
                ValidateInventoryCheckRow(row, errors, context, user);
                break;
            case "RepairSend":
                ValidateRepairSendRow(row, errors, context, user);
                break;
            case "BorrowLend":
                ValidateBorrowLendRow(row, errors, context, user);
                break;
            case "WarehouseStructure":
                ValidateWarehouseStructureRow(row, errors, context, user);
                break;
            case "ItemMaster":
                ValidateItemMasterRow(row, errors, context);
                break;
            case "ItemMasterUpdate":
                break;
            case "QuantityInbound":
            case "QuantityOutbound":
            case "QuantityAdjust":
                ValidateQuantityOperationRow(importType, row, errors, context, user);
                break;
            case "MoveLocation":
                ValidateMoveLocationRow(row, errors, context, user);
                break;
            case "BorrowReturn":
                ValidateBorrowReturnRow(row, errors, context);
                break;
            case "RepairReceive":
                ValidateRepairReceiveRow(row, errors, context);
                break;
        }

        return errors;
    }

    private static void ValidateInboundRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var item = FindPreloadedItem(context, Value(row, "ItemCode"));
        if (item == null)
        {
            errors.Add("ItemCode does not exist.");
        }
        else if (item.IsSerialManaged && string.IsNullOrWhiteSpace(Value(row, "SerialNumber")))
        {
            errors.Add("SerialNumber is required for serial-managed item.");
        }

        var warehouse = FindPreloadedWarehouse(context, Value(row, "WarehouseCode"));
        if (warehouse == null)
        {
            errors.Add("WarehouseCode does not exist.");
        }
        else if (!user.CanAccessWarehouse(warehouse.Id))
        {
            errors.Add("Current user cannot import into this warehouse.");
        }

        var bin = warehouse == null ? null : FindPreloadedBin(context, warehouse.Id, Value(row, "BinCode"), BinLocationUsageType.LocationTracked);
        if (warehouse != null && bin == null)
        {
            errors.Add("BinCode is invalid for warehouse.");
        }
        else if (warehouse != null)
        {
            if (bin != null && context.OccupiedBinIds.Contains(bin.Id))
            {
                errors.Add("BinCode already contains another active item.");
            }
        }

        if (item != null)
        {
            var serial = Value(row, "SerialNumber");
            if (!string.IsNullOrWhiteSpace(serial) && HasPreloadedInstance(context, item.ItemCode, serial))
            {
                errors.Add("SerialNumber already exists.");
            }
        }

        var usageType = Value(row, "UsageType");
        if (!string.IsNullOrWhiteSpace(usageType) && !Enum.TryParse<BinLocationUsageType>(usageType, true, out _))
        {
            errors.Add("UsageType must be LocationTracked or Quantity.");
        }
    }

    private static void ValidateInventoryCheckRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var warehouse = FindPreloadedWarehouse(context, Value(row, "WarehouseCode"));
        if (warehouse == null)
        {
            errors.Add("WarehouseCode does not exist.");
            return;
        }

        if (!user.CanAccessWarehouse(warehouse.Id))
        {
            errors.Add("Current user cannot check this warehouse.");
        }

        var itemCode = Value(row, "ItemCode");
        var serial = Value(row, "SerialNumber");
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            errors.Add("ItemCode is required.");
        }

        if (string.IsNullOrWhiteSpace(serial))
        {
            errors.Add("SerialNumber is required.");
        }
        else if (!string.IsNullOrWhiteSpace(itemCode) &&
                 context.DeletedInstanceKeys.Contains((NormalizeCode(itemCode), NormalizeCode(serial))))
        {
            errors.Add("Item instance has been deleted and cannot be used in inventory check.");
        }

        var binCode = NullIfEmpty(Value(row, "BinCode")) ?? NullIfEmpty(Value(row, "ActualBinCode"));
        if (string.IsNullOrWhiteSpace(binCode))
        {
            errors.Add("BinCode or ActualBinCode is required.");
        }
        else if (warehouse != null)
        {
            var bin = FindPreloadedBin(context, warehouse.Id, binCode, BinLocationUsageType.LocationTracked);
            if (bin == null)
            {
                errors.Add("BinCode does not exist in the checked warehouse.");
            }
        }
    }

    private static void ValidateBorrowLendRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var instance = FindPreloadedInstance(context, row);
        if (instance == null)
        {
            errors.Add("SerialNumber does not exist.");
        }
        else if (instance.Status != ItemStatus.InStock && instance.Status != ItemStatus.Normal)
        {
            errors.Add("Only InStock items can be lent.");
        }
        else
        {
            context.CurrentLocationsByInstanceId.TryGetValue(instance.Id, out var current);
            if (current?.WarehouseId != null && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                errors.Add("Current user cannot lend items from this warehouse.");
            }
        }

        if (string.IsNullOrWhiteSpace(Value(row, "BorrowerCode")))
        {
            errors.Add("BorrowerCode is required.");
        }
    }

    private static void ValidateRepairSendRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        if (!context.ActivePartyKeys.Contains((ExternalPartyType.RepairVendor, NormalizeCode(Value(row, "RepairVendorCode")))))
        {
            errors.Add("RepairVendorCode does not exist.");
        }

        if (string.IsNullOrWhiteSpace(Value(row, "TargetExternalLocation")))
        {
            errors.Add("TargetExternalLocation is required.");
        }

        var instance = FindPreloadedInstance(context, row);
        if (instance == null)
        {
            errors.Add("SerialNumber or Barcode does not exist.");
        }
        else if (instance.Status != ItemStatus.InStock && instance.Status != ItemStatus.Damaged && instance.Status != ItemStatus.Normal)
        {
            errors.Add("Only InStock or Damaged item can be sent to repair.");
        }
        else
        {
            context.CurrentLocationsByInstanceId.TryGetValue(instance.Id, out var current);
            if (current?.WarehouseId != null && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                errors.Add("Current user cannot send this item to repair.");
            }
        }
    }

    private static void ValidateWarehouseStructureRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var warehouseCode = Value(row, "WarehouseCode");
        var binCode = Value(row, "BinCode");
        if (!string.IsNullOrWhiteSpace(warehouseCode) && !string.IsNullOrWhiteSpace(binCode))
        {
            var warehouse = FindPreloadedWarehouse(context, warehouseCode);
            if (warehouse != null)
            {
                if (context.ExistingBinKeys.Contains((warehouse.Id, NormalizeCode(binCode))))
                {
                    errors.Add($"Bin code {binCode} already exists in warehouse {warehouseCode}.");
                }
                if (!user.CanAccessWarehouse(warehouse.Id))
                {
                    errors.Add($"Current user cannot manage warehouse {warehouseCode}.");
                }
            }
        }
    }

    private static void ValidateItemMasterRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context)
    {
        var itemCode = Value(row, "ItemCode");
        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            if (context.ExistingItemCodes.Contains(NormalizeCode(itemCode)))
            {
                errors.Add($"ItemCode {itemCode} already exists in the system.");
            }
        }
    }

    private async Task<ServiceResult<int>> ValidateItemMasterUpdateBatchAsync(ImportBatch batch, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var dataRows = batch.Rows.OrderBy(x => x.RowNumber).Select(x => new { Row = x, Data = Row(x) }).ToArray();
        var itemCodes = dataRows
            .Select(x => Value(x.Data, "ItemCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var newCategoryCodes = dataRows
            .Select(x => Value(x.Data, "NewCategoryCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var newUnitCodes = dataRows
            .Select(x => Value(x.Data, "NewUnitCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = await _db.Items
            .Include(x => x.Category)
            .Include(x => x.Unit)
            .Where(x => itemCodes.Contains(x.ItemCode) && x.IsActive)
            .ToDictionaryAsync(x => x.ItemCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categories = await _db.ItemCategories
            .Where(x => newCategoryCodes.Contains(x.CategoryCode) && x.IsActive)
            .ToDictionaryAsync(x => x.CategoryCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var units = await _db.ItemUnits
            .Where(x => newUnitCodes.Contains(x.UnitCode) && x.IsActive)
            .ToDictionaryAsync(x => x.UnitCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var duplicateCodes = dataRows
            .Select(x => Value(x.Data, "ItemCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var blocking = 0;
        foreach (var rowData in dataRows)
        {
            var errors = ValidateItemMasterUpdateRow(rowData.Data, items, categories, units, duplicateCodes);
            var row = rowData.Row;
            row.IsValid = errors.Count == 0;
            row.Severity = errors.Count == 0 ? ValidationSeverity.Info : ValidationSeverity.Blocking;
            row.Message = errors.Count == 0 ? BuildItemMasterUpdateSummary(rowData.Data, items, categories, units) : string.Join("; ", errors);
            row.SuggestedFix = errors.Count == 0 ? null : "Correct the row and upload again.";
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserName;
            if (errors.Count > 0)
            {
                blocking++;
            }
        }

        batch.BlockingErrorRows = blocking;
        batch.Status = blocking == 0 ? ImportBatchStatus.Validated : ImportBatchStatus.Blocked;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = user.UserName;
        var autoDetectChanges = _db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
        return ServiceResult<int>.Ok(blocking, blocking == 0 ? "Import batch is valid." : "Import batch has blocking errors.");
    }

    private static List<string> ValidateItemMasterUpdateRow(
        Dictionary<string, string> row,
        IReadOnlyDictionary<string, Item> items,
        IReadOnlyDictionary<string, ItemCategory> categories,
        IReadOnlyDictionary<string, ItemUnit> units,
        IReadOnlySet<string> duplicateCodes)
    {
        var errors = RequiredColumns("ItemMasterUpdate")
            .Where(x => string.IsNullOrWhiteSpace(Value(row, x)))
            .Select(x => $"{x} is required.")
            .ToList();
        var itemCode = Value(row, "ItemCode");
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return errors;
        }

        if (duplicateCodes.Contains(itemCode))
        {
            errors.Add("Duplicate ItemCode in import file.");
        }

        if (!HasItemMasterUpdateField(row))
        {
            errors.Add("No update field specified.");
        }

        if (!items.TryGetValue(itemCode, out var item))
        {
            errors.Add("ItemCode does not exist.");
            return errors;
        }

        var currentCategoryCode = Value(row, "CurrentCategoryCode");
        if (!string.IsNullOrWhiteSpace(currentCategoryCode) &&
            !string.Equals(item.Category?.CategoryCode, currentCategoryCode, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Current category mismatch. Expected {currentCategoryCode} but actual {item.Category?.CategoryCode ?? "-"}.");
        }

        var newCategoryCode = Value(row, "NewCategoryCode");
        if (!string.IsNullOrWhiteSpace(newCategoryCode) && !categories.ContainsKey(newCategoryCode))
        {
            errors.Add("NewCategoryCode does not exist.");
        }

        var currentUnitCode = Value(row, "CurrentUnitCode");
        if (!string.IsNullOrWhiteSpace(currentUnitCode) &&
            !string.Equals(item.Unit?.UnitCode, currentUnitCode, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Current unit mismatch. Expected {currentUnitCode} but actual {item.Unit?.UnitCode ?? "-"}.");
        }

        var newUnitCode = Value(row, "NewUnitCode");
        if (!string.IsNullOrWhiteSpace(newUnitCode) && !units.ContainsKey(newUnitCode))
        {
            errors.Add("NewUnitCode does not exist.");
        }

        var serialValue = Value(row, "NewIsSerialManaged");
        if (!string.IsNullOrWhiteSpace(serialValue) && !TryParseBool(serialValue, out _))
        {
            errors.Add("NewIsSerialManaged must be yes/no, true/false, or 1/0.");
        }

        return errors;
    }

    // ─── New Validate Methods ────────────────────────────────────────────────

    private static void ValidateQuantityOperationRow(string operationType, Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var warehouseCode = Value(row, "WarehouseCode");
        var warehouse = FindPreloadedWarehouse(context, warehouseCode);
        if (warehouse == null) { errors.Add("WarehouseCode does not exist."); return; }
        if (!user.CanAccessWarehouse(warehouse.Id)) { errors.Add("Current user cannot access this warehouse."); return; }

        var itemCode = Value(row, "ItemCode").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(itemCode)) { errors.Add("ItemCode is required."); return; }

        var snCode = Value(row, "SnCode").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(snCode)) { errors.Add("SnCode is required."); return; }

        if (!decimal.TryParse(Value(row, "Quantity"), out var qty) || qty <= 0)
        {
            errors.Add("Quantity must be a positive number.");
        }

        // For Outbound: check balance exists
        if (operationType == "Issue" || operationType == "Adjust")
        {
            var item = FindPreloadedItem(context, itemCode);
            if (item != null)
            {
                var balanceExists = context.QuantityBalanceKeys.Contains((warehouse.Id, item.Id, snCode));
                if (!balanceExists && operationType == "Issue")
                    errors.Add($"SnCode '{snCode}' has no stock balance in this warehouse.");
            }
        }
    }

    private static void ValidateMoveLocationRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context, CurrentUserContext user)
    {
        var instance = FindPreloadedInstance(context, row);
        if (instance == null) { errors.Add("SerialNumber or Barcode does not exist."); return; }

        context.CurrentLocationsByInstanceId.TryGetValue(instance.Id, out var current);
        if (current?.WarehouseId != null && !user.CanAccessWarehouse(current.WarehouseId.Value))
            errors.Add("Current user cannot move items from this warehouse.");

        var targetWarehouseCode = Value(row, "TargetWarehouseCode");
        var targetWarehouse = FindPreloadedWarehouse(context, targetWarehouseCode);
        if (targetWarehouse == null) { errors.Add("TargetWarehouseCode does not exist."); return; }
        if (!user.CanAccessWarehouse(targetWarehouse.Id)) errors.Add("Current user cannot move items to this warehouse.");

        var targetBin = FindPreloadedBin(context, targetWarehouse.Id, Value(row, "TargetBinCode"), BinLocationUsageType.LocationTracked);
        if (targetBin == null) errors.Add("TargetBinCode does not exist in target warehouse.");
    }

    private static void ValidateBorrowReturnRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context)
    {
        var instance = FindPreloadedInstance(context, row);
        if (instance == null) { errors.Add("SerialNumber or Barcode does not exist."); return; }
        if (instance.Status != ItemStatus.LentOut) errors.Add("Item is not currently lent out.");

        var borrowDocNo = NullIfEmpty(Value(row, "BorrowDocumentNo"));
        if (borrowDocNo != null)
        {
            var docExists = context.BorrowDocumentNos.Contains(NormalizeCode(borrowDocNo));
            if (!docExists) errors.Add($"BorrowDocumentNo '{borrowDocNo}' not found.");
        }
    }

    private static void ValidateRepairReceiveRow(Dictionary<string, string> row, List<string> errors, ImportValidationContext context)
    {
        var instance = FindPreloadedInstance(context, row);
        if (instance == null) { errors.Add("SerialNumber or Barcode does not exist."); return; }
        if (instance.Status != ItemStatus.Repairing) errors.Add("Item is not currently under repair.");

        var repairDocNo = NullIfEmpty(Value(row, "RepairDocumentNo"));
        if (repairDocNo != null)
        {
            var docExists = context.RepairDocumentNos.Contains(NormalizeCode(repairDocNo));
            if (!docExists) errors.Add($"RepairDocumentNo '{repairDocNo}' not found.");
        }

        var targetWarehouse = FindPreloadedWarehouse(context, Value(row, "TargetWarehouseCode"));
        if (targetWarehouse == null) { errors.Add("TargetWarehouseCode does not exist."); return; }
        var targetBin = FindPreloadedBin(context, targetWarehouse.Id, Value(row, "TargetBinCode"), BinLocationUsageType.LocationTracked);
        if (targetBin == null) errors.Add("TargetBinCode does not exist in target warehouse.");
    }

    // ─── New Confirm Methods ─────────────────────────────────────────────────

    private async Task<int> ConfirmQuantityOperationImportAsync(string operationType, IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        // Quantity service uses ItemCode at header level, so split blank-document imports by item/warehouse.
        foreach (var group in rows.GroupBy(x => new
        {
            DocumentNo = NullIfEmpty(Value(x, "DocumentNo")),
            WarehouseCode = NormalizeCode(Value(x, "WarehouseCode")),
            ItemCode = NormalizeCode(Value(x, "ItemCode")),
            ItemCategoryCode = NormalizeCode(Value(x, "ItemCategoryCode")),
            OwnerName = NullIfEmpty(Value(x, "OwnerName"))
        }))
        {
            var firstRow = group.First();
            var warehouseCode = Value(firstRow, "WarehouseCode");
            var warehouse = await FindWarehouseAsync(warehouseCode, cancellationToken)
                            ?? throw new InvalidOperationException($"Warehouse '{warehouseCode}' not found.");

            var itemCategoryCode = NullIfEmpty(Value(firstRow, "ItemCategoryCode")) ?? "GENERAL";
            var itemCode         = Value(firstRow, "ItemCode").Trim().ToUpperInvariant();
            var ownerName        = NullIfEmpty(Value(firstRow, "OwnerName"));
            var docNo            = group.Key.DocumentNo ?? _documentNumbers.Next("QTY", DateTime.UtcNow);
            DateTime.TryParse(Value(firstRow, "DocumentDate"), out var docDate);
            if (docDate == default) docDate = DateTime.UtcNow;

            var lines = group.Select(row =>
            {
                decimal.TryParse(Value(row, "Quantity"), out var qty);
                return new QuantityInventoryLineRequest
                {
                    ItemCategoryCode    = NullIfEmpty(Value(row, "ItemCategoryCode")) ?? "GENERAL",
                    ItemCode            = Value(row, "ItemCode").Trim().ToUpperInvariant(),
                    SnCode = Value(row, "SnCode").Trim().ToUpperInvariant(),
                    Quantity = qty,
                    Status   = string.IsNullOrEmpty(Value(row, "Status")) ? Value(row, "Status") : ItemStatus.Normal.ToString(),
                    Note     = NullIfEmpty(Value(row, "Note"))
                };
            }).ToList();

            var request = new QuantityInventoryRequest
            {
                DocumentNo          = docNo,
                DocumentDate        = docDate,
                WarehouseId         = warehouse.Id,
                //ItemCategoryCode    = itemCategoryCode,
                //ItemCode            = itemCode,
                OwnerName           = ownerName,
                Note                = NullIfEmpty(Value(firstRow, "Note")),
                Lines               = lines
            };

            var result = operationType switch
            {
                "Receive" => await _quantityInventoryService.ReceiveAsync(request, user, cancellationToken),
                "Issue"   => await _quantityInventoryService.IssueAsync(request, user, cancellationToken),
                _         => await _quantityInventoryService.AdjustAsync(request, user, cancellationToken),
            };

            if (!result.Success)
                throw new InvalidOperationException($"Quantity {operationType} failed: {result.Message}");

            count += lines.Count;
        }

        return count;
    }

    private async Task<int> ConfirmMoveLocationImportAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        var context = await BuildImportValidationContextAsync("MoveLocation", rows, cancellationToken);
        foreach (var group in rows.GroupBy(row => new
        {
            TargetWarehouseCode = NormalizeCode(Value(row, "TargetWarehouseCode")),
            DocumentDate = ParseImportDate(Value(row, "DocumentDate")),
            Note = NullIfEmpty(Value(row, "Note"))
        }))
        {
            var firstRow = group.First();
            var targetWarehouse = FindPreloadedWarehouse(context, Value(firstRow, "TargetWarehouseCode"))
                ?? throw new InvalidOperationException("Target warehouse not found.");

            foreach (var batch in SplitByUniqueTargetBin(group))
            {
                var lines = batch.Select(row =>
                {
                    var instance = FindPreloadedInstance(context, row) ?? throw new InvalidOperationException("Item instance not found.");
                    var targetBin = FindPreloadedBin(context, targetWarehouse.Id, Value(row, "TargetBinCode"), BinLocationUsageType.LocationTracked)
                        ?? throw new InvalidOperationException("Target bin not found.");

                    return new MoveLocationLineRequest
                    {
                        SerialNumber = instance.SerialNumber ?? string.Empty,
                        ItemCode = instance.Item?.ItemCode ?? Value(row, "ItemCode"),
                        TargetBinCode = targetBin.BinCode,
                        Note = NullIfEmpty(Value(row, "Note"))
                    };
                }).ToArray();

                var request = new MoveLocationRequest
                {
                    WarehouseId = targetWarehouse.Id,
                    WarehouseCode = targetWarehouse.WarehouseCode,
                    DocumentDate = group.Key.DocumentDate,
                    Lines = lines,
                    Note = group.Key.Note
                };
                var result = await _moveLocationService.MoveLocationAsync(request, user, cancellationToken);
                if (!result.Success) throw new InvalidOperationException($"MoveLocation failed: {result.Message}");
                count += lines.Length;
            }
        }
        return count;
    }

    private async Task<int> ConfirmBorrowReturnImportAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => NullIfEmpty(Value(x, "BorrowDocumentNo"))))
        {
            var firstRow = group.First();
            var request = new BorrowReturnRequest
            {
                BorrowDocumentNo  = group.Key,
                ReturnLocationBinCode = NullIfEmpty(Value(firstRow, "ReturnLocationBinCode")),
                Note              = NullIfEmpty(Value(firstRow, "Note")),
                Lines             = group.Select(row =>
                {
                    var instance = FindInstanceAsync(row, cancellationToken).GetAwaiter().GetResult();
                    return new BorrowReturnLineRequest
                    {
                        ItemCode = instance?.Item?.ItemCode ?? Value(row, "ItemCode"),
                        SerialNumber = NullIfEmpty(Value(row, "SerialNumber")) ?? string.Empty,
                        Condition = Enum.TryParse<BorrowReturnCondition>(Value(row, "Condition"), true, out var condition) ? condition : BorrowReturnCondition.Normal,
                        TargetBinCode = NullIfEmpty(Value(row, "ReturnLocationBinCode")),
                        Note = NullIfEmpty(Value(row, "Note"))
                    };
                }).ToList()
            };
            var result = await _borrowService.ReturnAsync(request, user, cancellationToken);
            if (!result.Success) throw new InvalidOperationException($"BorrowReturn failed: {result.Message}");
            count += request.Lines.Count;
        }
        return count;
    }

    private async Task<int> ConfirmRepairReceiveImportAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        var context = await BuildImportValidationContextAsync("RepairReceive", rows, cancellationToken);
        foreach (var group in rows.GroupBy(row => new
        {
            RepairDocumentNo = NullIfEmpty(Value(row, "RepairDocumentNo")),
            TargetWarehouseCode = NormalizeCode(Value(row, "TargetWarehouseCode"))
        }))
        {
            var firstRow = group.First();
            var targetWarehouse = FindPreloadedWarehouse(context, Value(firstRow, "TargetWarehouseCode"))
                ?? throw new InvalidOperationException("Target warehouse not found.");

            foreach (var batch in SplitByUniqueTargetBin(group))
            {
                var firstBatchRow = batch.First();
                var firstTargetBin = FindPreloadedBin(context, targetWarehouse.Id, Value(firstBatchRow, "TargetBinCode"), BinLocationUsageType.LocationTracked)
                    ?? throw new InvalidOperationException("Target bin not found.");
                var lines = batch.Select(row =>
                {
                    var instance = FindPreloadedInstance(context, row) ?? throw new InvalidOperationException("Item instance not found.");
                    var targetBin = FindPreloadedBin(context, targetWarehouse.Id, Value(row, "TargetBinCode"), BinLocationUsageType.LocationTracked)
                        ?? throw new InvalidOperationException("Target bin not found.");
                    Enum.TryParse<ItemStatus>(Value(row, "NewStatus"), true, out var newStatus);
                    if (newStatus == default) newStatus = ItemStatus.Normal;
                    var repairResult = Enum.TryParse<RepairResult>(Value(row, "NewStatus"), true, out var parsedRepairResult)
                        ? parsedRepairResult
                        : newStatus is ItemStatus.Damaged or ItemStatus.Scrapped or ItemStatus.Lost ? RepairResult.Failed : RepairResult.Success;

                    return new RepairReceiveLineRequest
                    {
                        SerialNumber = instance.SerialNumber ?? string.Empty,
                        ItemCode = instance.Item?.ItemCode ?? string.Empty,
                        Result = repairResult,
                        TargetBinCode = targetBin.BinCode,
                        Note = NullIfEmpty(Value(row, "Note"))
                    };
                }).ToArray();

                var request = new RepairReceiveRequest
                {
                    RepairDocumentNo = group.Key.RepairDocumentNo ?? string.Empty,
                    TargetWarehouseId = targetWarehouse.Id,
                    TargetBinCode = firstTargetBin.BinCode,
                    Note = NullIfEmpty(Value(firstBatchRow, "Note")),
                    Lines = lines
                };
                var result = await _repairService.ReceiveFromRepairAsync(request, user, cancellationToken);
                if (!result.Success) throw new InvalidOperationException($"RepairReceive failed: {result.Message}");
                count += lines.Length;
            }
        }
        return count;
    }

    private static IEnumerable<IReadOnlyCollection<Dictionary<string, string>>> SplitByUniqueTargetBin(IEnumerable<Dictionary<string, string>> rows)
    {
        var batch = new List<Dictionary<string, string>>();
        var targetBins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var targetBin = NormalizeCode(Value(row, "TargetBinCode"));
            if (batch.Count > 0 && !targetBins.Add(targetBin))
            {
                yield return batch;
                batch = new List<Dictionary<string, string>>();
                targetBins.Clear();
                targetBins.Add(targetBin);
            }
            else if (batch.Count == 0)
            {
                targetBins.Add(targetBin);
            }

            batch.Add(row);
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private static DateTime ParseImportDate(string value)
        => DateTime.TryParse(value, out var parsed) ? parsed : DateTime.UtcNow.Date;

    private async Task<int> ConfirmItemMasterAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var row in rows)
        {
            var itemCode = Value(row, "ItemCode");
            if (await _db.Items.AnyAsync(x => x.ItemCode == itemCode, cancellationToken))
            {
                continue;
            }

            var category = await EnsureCategoryAsync(Value(row, "CategoryCode"), Value(row, "CategoryName") ?? Value(row, "CategoryCode"), user, cancellationToken);
            var unit = await EnsureUnitAsync(Value(row, "UnitCode"), Value(row, "UnitName") ?? Value(row, "UnitCode"), user, cancellationToken);
            var item = new Item
            {
                ItemCode = itemCode,
                DefaultName = Value(row, "DefaultName"),
                CategoryId = category.Id,
                UnitId = unit.Id,
                IsSerialManaged = Bool(Value(row, "IsSerialManaged")),
                CreatedBy = user.UserName
            };
            AddItemTranslation(item, "vi", Value(row, "NameVi"), user);
            AddItemTranslation(item, "en", Value(row, "NameEn"), user);
            AddItemTranslation(item, "zh", Value(row, "NameZh"), user);
            _db.Items.Add(item);
            count++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ConfirmItemMasterUpdateAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var itemCodes = rows
            .Select(x => Value(x, "ItemCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var newCategoryCodes = rows
            .Select(x => Value(x, "NewCategoryCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var newUnitCodes = rows
            .Select(x => Value(x, "NewUnitCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = await _db.Items
            .Include(x => x.Category)
            .Include(x => x.Unit)
            .Where(x => itemCodes.Contains(x.ItemCode) && x.IsActive)
            .ToDictionaryAsync(x => x.ItemCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categories = await _db.ItemCategories
            .Where(x => newCategoryCodes.Contains(x.CategoryCode) && x.IsActive)
            .ToDictionaryAsync(x => x.CategoryCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var units = await _db.ItemUnits
            .Where(x => newUnitCodes.Contains(x.UnitCode) && x.IsActive)
            .ToDictionaryAsync(x => x.UnitCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var instances = await _db.ItemInstances
            .Where(x => itemCodes.Contains(x.Item!.ItemCode) && x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var instancesByItemId = instances.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.ToList());

        var changedItems = new List<Item>();
        var auditLogs = new List<AuditLog>();
        foreach (var row in rows)
        {
            if (!items.TryGetValue(Value(row, "ItemCode"), out var item))
            {
                continue;
            }

            IReadOnlyCollection<ItemInstance> itemInstances = instancesByItemId.TryGetValue(item.Id, out var loadedInstances)
                ? loadedInstances
                : Array.Empty<ItemInstance>();
            var before = BuildItemMasterAuditSnapshot(item, itemInstances);
            var changed = false;

            var newCategoryCode = Value(row, "NewCategoryCode");
            if (!string.IsNullOrWhiteSpace(newCategoryCode) && categories.TryGetValue(newCategoryCode, out var category) && item.CategoryId != category.Id)
            {
                item.CategoryId = category.Id;
                item.Category = category;
                changed = true;
            }

            var newUnitCode = Value(row, "NewUnitCode");
            if (!string.IsNullOrWhiteSpace(newUnitCode) && units.TryGetValue(newUnitCode, out var unit) && item.UnitId != unit.Id)
            {
                item.UnitId = unit.Id;
                item.Unit = unit;
                changed = true;
            }

            var newDefaultName = Value(row, "NewDefaultName");
            if (!string.IsNullOrWhiteSpace(newDefaultName) && !string.Equals(item.DefaultName, newDefaultName, StringComparison.Ordinal))
            {
                item.DefaultName = newDefaultName;
                changed = true;
            }

            var newIsSerialManaged = Value(row, "NewIsSerialManaged");
            if (!string.IsNullOrWhiteSpace(newIsSerialManaged) && TryParseBool(newIsSerialManaged, out var parsedSerialManaged) && item.IsSerialManaged != parsedSerialManaged)
            {
                item.IsSerialManaged = parsedSerialManaged;
                changed = true;
            }

            var ownerChanged = false;
            var newOwnerName = Value(row, "NewOwnerName");
            if (!string.IsNullOrWhiteSpace(newOwnerName))
            {
                foreach (var instance in itemInstances)
                {
                    if (!string.Equals(instance.OwnerName, newOwnerName, StringComparison.Ordinal))
                    {
                        instance.OwnerName = newOwnerName;
                        instance.UpdatedAt = DateTime.UtcNow;
                        instance.UpdatedBy = user.UserName;
                        ownerChanged = true;
                    }
                }
            }

            if (!changed && !ownerChanged)
            {
                continue;
            }

            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = user.UserName;
            changedItems.Add(item);
            var after = BuildItemMasterAuditSnapshot(item, itemInstances);
            auditLogs.Add(new AuditLog
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Action = "UpdateItemMasterByImport",
                EntityName = "Item",
                EntityId = item.Id,
                ReferenceNo = item.ItemCode,
                BeforeJson = JsonSerializer.Serialize(before, JsonOptions),
                AfterJson = JsonSerializer.Serialize(after, JsonOptions),
                Result = "Success",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (auditLogs.Count > 0)
        {
            _db.AuditLogs.AddRange(auditLogs);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return changedItems.Count;
    }

    private async Task<int> ConfirmWarehouseStructureAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var row in rows)
        {
            var company = await EnsureCompanyAsync(Value(row, "CompanyCode"), Value(row, "CompanyName"), user, cancellationToken);
            var branch = await EnsureBranchAsync(company.Id, Value(row, "BranchCode"), Value(row, "BranchName"), user, cancellationToken);
            var warehouse = await EnsureWarehouseAsync(branch.Id, Value(row, "WarehouseCode"), Value(row, "WarehouseName"), user, cancellationToken);
            var zone = await EnsureZoneAsync(warehouse.Id, Value(row, "ZoneCode"), Value(row, "ZoneName"), user, cancellationToken);
            var rack = await EnsureRackAsync(zone.Id, Value(row, "RackCode"), Value(row, "RackName"), user, cancellationToken);
            var shelf = await EnsureShelfAsync(rack.Id, Value(row, "ShelfCode"), Value(row, "ShelfName"), user, cancellationToken);
            var binCode = Value(row, "BinCode");
            var usageType = ParseBinUsageType(Value(row, "UsageType"));
            if (!await _db.BinLocations.AnyAsync(x => x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
            {
                _db.BinLocations.Add(new BinLocation
                {
                    WarehouseId = warehouse.Id,
                    ShelfId = shelf.Id,
                    BinCode = binCode,
                    FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}",
                    UsageType = usageType,
                    CreatedBy = user.UserName
                });
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task<int> ConfirmInboundAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => NullIfEmpty(Value(x, "DocumentNo")) ?? "auto", StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var warehouse = await FindWarehouseAsync(Value(first, "WarehouseCode"), cancellationToken)
                ?? throw new InvalidOperationException("Warehouse not found.");

            var mixedWarehouse = group
                .Select(x => Value(x, "WarehouseCode"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any();
            if (mixedWarehouse)
            {
                throw new InvalidOperationException($"Inbound document {group.Key} contains multiple warehouses.");
            }

            var request = new InboundRequest
            {
                DocumentNo = group.Key,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseId = warehouse.Id,
                DocumentDate = DateTime.Parse(Value(first, "DocumentDate")),
                SourcePartyer = NullIfEmpty(Value(first, "SourcePartyCode")),
                ReceiverCode = Value(first, "PartyCode"),
                ReceiverName = Value(first, "Name"),
                ReceiverPhone = Value(first, "Phone"),
                ReceiverDepartment = Value(first, "Department"),
                ApprovedBy = user.UserName,
                Note = NullIfEmpty(Value(first, "Note")),
                OwnerName = NullIfEmpty(Value(first, "OwnerName")),
                Lines = group.Select(row => new InboundLineRequest
                {
                    ItemCode = Value(row, "ItemCode"),
                    SerialNumber = NullIfEmpty(Value(row, "SerialNumber")),
                    MT = NullIfEmpty(Value(row, "MT")),
                    Quantity = 1,
                    BinCode = Value(row, "BinCode"),
                    Condition = NullIfEmpty(Value(row, "Condition")),
                    Note = NullIfEmpty(Value(row, "Note"))
                }).ToArray()
            };

            var result = await _inboundService.CreateInboundAsync(request, user, cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Inbound failed: {string.Join("; ", result.Errors)}");
            }

            count += request.Lines.Count;
        }

        return count;
    }

    private async Task<int> ConfirmInboundBulkLegacyAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        {
            var now = DateTime.UtcNow;
            var count = 0;

            _db.ChangeTracker.AutoDetectChangesEnabled = false;

            var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var serial = NullIfEmpty(Value(row, "SerialNumber"));
                if (serial != null && !serials.Add(serial))
                    throw new InvalidOperationException($"Serial {serial} duplicated in file.");

                var barcode = NullIfEmpty(Value(row, "Barcode"));
                if (barcode != null && !barcodes.Add(barcode))
                    throw new InvalidOperationException($"Barcode {barcode} duplicated in file.");
            }

            var itemCodes = rows.Select(r => Value(r, "ItemCode").ToUpper()).Distinct().ToList();
            var binCodes = rows.Select(r => Value(r, "BinCode")).Distinct().ToList();
            var warehouseCodes = rows.Select(r => Value(r, "WarehouseCode")).Distinct().ToList();

            var items = await _db.Items
                .Where(x => itemCodes.Contains(x.ItemCode))
                .ToDictionaryAsync(x => x.ItemCode, cancellationToken);

            var warehouses = await _db.Warehouses
                .Where(x => warehouseCodes.Contains(x.WarehouseCode))
                .ToDictionaryAsync(x => x.WarehouseCode, cancellationToken);

            var bins = await _db.BinLocations
                .Where(x => binCodes.Contains(x.BinCode))
                .ToListAsync(cancellationToken);

            var binDict = bins.ToDictionary(x => (x.WarehouseId, x.BinCode));

            // check serial tồn tại DB
            var existingSerials = await _db.ItemInstances
                .Where(x => serials.Contains(x.SerialNumber!) && !x.IsDeleted)
                .Select(x => new { x.SerialNumber, x.ItemId })
                .ToListAsync(cancellationToken);

            if (existingSerials.Any())
                throw new InvalidOperationException($"Serial exists: {string.Join(",", existingSerials)}");

            var documents = new List<InboundDocument>();
            var instances = new List<ItemInstance>();
            var lines = new List<InboundDocumentLine>();
            var locations = new List<CurrentItemLocation>();
            var externalParties = new List<ExternalParty>();
            var inboundDocumentLog = new List<InboundDocumentLog>();

            foreach (var group in rows.GroupBy(x => Value(x, "DocumentNo")))
            {
                var docNo = group.Key;
                if (string.IsNullOrWhiteSpace(docNo))
                    docNo = _documentNumbers.Next("INB", now);

                var first = group.First();

                if (!warehouses.TryGetValue(Value(first, "WarehouseCode"), out var warehouse))
                    throw new InvalidOperationException("Warehouse not found.");

                var document = new InboundDocument
                {
                    DocumentNo = docNo,
                    DocumentDate = DateTime.Parse(Value(first, "DocumentDate")),
                    WarehouseId = warehouse.Id,
                    CreatedBy = user.UserName,
                    ApprovedBy = user.UserName,
                    ApprovedAt = DateTime.Parse(Value(first, "DocumentDate")),
                    PostedAt = DateTime.Parse(Value(first, "DocumentDate")),
                    PartyDepartment = Value(first, "Department"),
                    DepartmentOwner = Value(first, "Department"),
                };

                documents.Add(document);

                foreach (var row in group)
                {
                    if (!items.TryGetValue(Value(row, "ItemCode").ToUpper(), out var item))
                        throw new InvalidOperationException("Item not found.");

                    if (!binDict.TryGetValue((warehouse.Id, Value(row, "BinCode")), out var bin))
                        throw new InvalidOperationException("Bin not found.");

                    var isQtyOnly = Value(row, "TrackingType").Equals("QuantityOnly", StringComparison.OrdinalIgnoreCase);
                    var serial = NullIfEmpty(Value(row, "SerialNumber"));

                    var instance = new ItemInstance
                    {
                        ItemId       = item.Id,
                        SerialNumber = serial,
                        MT           = Value(row, "MT"),
                        Barcode      = serial,
                        DocumentNo   = docNo,
                        Status       = Enum.Parse<ItemStatus>(Value(row, "Condition")),
                        TrackingType = isQtyOnly ? ItemTrackingType.QuantityOnly : ItemTrackingType.LocationTracked,
                        OwnerName    = NullIfEmpty(Value(row, "OwnerName")),
                        CreatedBy    = user.UserName,
                        CreatedAt    = DateTime.Parse(Value(row, "DocumentDate")),
                    };

                    instances.Add(instance);

                    var line = new InboundDocumentLine
                    {
                        InboundDocument = document,
                        ItemId = item.Id,
                        ItemInstance = instance,
                        SerialNumber = serial,
                        Barcode = serial,
                        Quantity = 1,
                        Condition = Value(row, "Condition"),
                        Note = Value(row, "Note"),
                        BinLocationId = bin.Id,
                        CreatedBy = user.UserName
                    };

                    lines.Add(line);

                    locations.Add(new CurrentItemLocation
                    {
                        ItemInstance = instance,
                        WarehouseId = warehouse.Id,
                        BinLocationId = bin.Id,
                        LocationType = LocationType.BinLocation,
                        ReferenceDocumentNo = docNo,
                        ReferenceDocumentType = nameof(InboundDocument),
                        UpdatedLocationAt = DateTime.Parse(Value(row, "DocumentDate")),
                        UpdatedLocationBy = user.UserName,
                        CreatedBy = user.UserName
                    });

                    inboundDocumentLog.Add(new InboundDocumentLog
                    {
                        InboundDocument = document,
                        ItemInstance = instance,
                        Action = "InboundReceive",
                        OldStatus = "Reserved",
                        NewStatus = line.Condition,
                        Receiver = $"{Value(row, "PartyCode")}-{Value(row, "Name")}",
                        ReceiverPhone = Value(row, "Phone"),
                        ReceiverDepartment = Value(row, "Department"),
                        DepartmentOwner = Value(row, "Department"),
                        OldLocationText = "Supplier",
                        NewLocationText = bin.FullPath,
                        PerformedBy = user.UserName,
                        Timestamp = DateTime.Parse(Value(row, "DocumentDate")),
                        Note = Value(row, "Note")
                    });

                    var partyCode = $"{Value(row, "PartyCode")}-{Value(row, "Name")}";
                    var existingParty = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.PartyCode == partyCode && x.PartyType == ExternalPartyType.Supplier, cancellationToken);
                    if (existingParty == null)
                    {
                        externalParties.Add(new ExternalParty
                        {
                            PartyCode = partyCode,
                            Name = Value(row, "Name"),
                            PartyType = ExternalPartyType.Supplier,
                            Phone = Value(row, "Phone"),
                            CreatedBy = user.UserName
                        });
                        _db.ExternalParties.Add(externalParties.Last());
                        _db.SaveChanges();
                    }

                    count++;
                }
            }

            var config = new BulkConfig
            {
                SetOutputIdentity = true,
                BatchSize = 5000
            };

            await _db.BulkInsertAsync(instances, config);
            await _db.BulkInsertAsync(documents, config);
            await _db.BulkInsertAsync(externalParties, config);

            foreach (var log in inboundDocumentLog)
            {
                log.InboundDocumentId = log.InboundDocument.Id;
                log.ItemInstanceId = log.ItemInstance.Id;
            }
            await _db.BulkInsertAsync(inboundDocumentLog, config);

            // map FK
            foreach (var doc in documents)
            {
                var party = externalParties.FirstOrDefault(x => x.Name == $"{Value(rows.First(r => Value(r, "DocumentNo") == doc.DocumentNo), "Name")}");
                if (party != null)
                {
                    doc.SourceExternalPartyId = party.Id;
                }
            }

            foreach (var line in lines)
            {
                line.InboundDocumentId = line.InboundDocument.Id;
                line.ItemInstanceId = line.ItemInstance.Id;
            }

            foreach (var loc in locations)
            {
                var document = documents.First(x => x.DocumentNo == loc.ReferenceDocumentNo);
                loc.ItemInstanceId = loc.ItemInstance.Id;
                loc.ReferenceDocumentId = document.Id;
            }

            foreach (var log in inboundDocumentLog)
            {
                log.InboundDocumentId = log.InboundDocument.Id;
                log.ItemInstanceId = log.ItemInstance.Id;
            }

            await _db.BulkInsertAsync(lines);
            await _db.BulkInsertAsync(locations);

            var histories = new List<ItemMovementHistory>();
            var transactions = new List<InventoryTransaction>();

            foreach (var line in lines)
            {
                var document = documents.First(x => x.Id == line.InboundDocumentId);
                var bin = bins.First(x => x.Id == line.BinLocationId);

                histories.Add(new ItemMovementHistory
                {
                    ItemInstanceId = line.ItemInstanceId.Value,
                    ActionType = MovementActionType.Inbound,
                    FromLocationDisplay = "Excel import",
                    ToLocationId = line.BinLocationId,
                    ToLocationType = LocationType.BinLocation,
                    ToLocationDisplay = bin.FullPath,
                    OldStatus = ItemStatus.Reserved,
                    NewStatus = ItemStatus.Normal,
                    DocumentType = nameof(InboundDocument),
                    DocumentId = document.Id,
                    DocumentNo = document.DocumentNo,
                    PerformedAt = line.CreatedAt,
                    PerformedBy = user.UserName
                });

                transactions.Add(new InventoryTransaction
                {
                    TransactionType = InventoryTransactionType.Inbound,
                    ItemId = line.ItemId,
                    ItemInstanceId = line.ItemInstanceId.Value,
                    WarehouseId = document.WarehouseId,
                    BinLocationId = line.BinLocationId,
                    QuantityDelta = 1,
                    StatusAfter = ItemStatus.Normal,
                    DocumentType = nameof(InboundDocument),
                    DocumentId = document.Id,
                    DocumentNo = document.DocumentNo,
                    PostedBy = user.UserName,
                    PostedAt = line.CreatedAt,
                });

            }

            await _db.BulkInsertAsync(histories);
            await _db.BulkInsertAsync(transactions);

            _db.ChangeTracker.AutoDetectChangesEnabled = true;

            return count;
        }
    }

    //private async Task<int> ConfirmInventoryCheckAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    //{
    //    var count = 0;
    //    DateTime now = DateTime.UtcNow;
    //    var listDocument = new List<InventoryCheckDocument>();
    //    foreach (var group in rows.GroupBy(x => Value(x, "WarehouseCode")))
    //    {
    //        var warehouse = await FindWarehouseAsync(group.Key, cancellationToken) ?? throw new InvalidOperationException("Warehouse not found.");
    //        var document = new InventoryCheckDocument { DocumentNo = _documentNumbers.Next("CHK", now), DocumentDate = now, WarehouseId = warehouse.Id, SessionStatus = "Finalized", CountMethod = "Excel", ResponsibleStaff = user.UserName, CreatedBy = user.UserName, ApprovedBy = user.UserName, ApprovedAt = DateTime.UtcNow, PostedAt = DateTime.UtcNow };
    //        _db.InventoryCheckDocuments.Add(document);
    //        await _db.SaveChangesAsync(cancellationToken);
    //        foreach (var row in group)
    //        {
    //            var instance = await FindInstanceAsync(row, cancellationToken);
    //            var binCode = NullIfEmpty(Value(row, "BinCode")) ?? NullIfEmpty(Value(row, "ActualBinCode"));
    //            var actualBin = binCode != null ? await FindBinAsync(warehouse.Id, binCode, cancellationToken) : null;
    //            var current = instance == null ? null : await _db.CurrentItemLocations.FirstOrDefaultAsync(x => x.ItemInstanceId == instance.Id, cancellationToken);

    //            // Auto-determine result like manual flow
    //            InventoryCheckLineResult result;
    //            if (instance == null && actualBin != null)
    //            {
    //                var item = await FindItemByCodeAsync(Value(row, "ItemCode"), cancellationToken);
    //                if (item == null)
    //                {
    //                    _db.InventoryCheckLines.Add(new InventoryCheckLine
    //                    {
    //                        InventoryCheckDocumentId = document.Id,
    //                        SystemBinLocationId = null,
    //                        ActualBinLocationId = actualBin?.Id,
    //                        Result = InventoryCheckLineResult.Extra,
    //                        Note = Value(row, "Note"),
    //                        CreatedAt = now,
    //                        CreatedBy = user.UserName
    //                    });
    //                    count++;
    //                    continue;
    //                }
    //                var newInstance = new ItemInstance
    //                {
    //                    ItemId = item.Id,
    //                    SerialNumber = Value(row, "SerialNumber").Trim(),
    //                    Barcode = Value(row, "SerialNumber").Trim(),
    //                    Status = ItemStatus.Normal,
    //                    CreatedAt = now,
    //                    CreatedBy = user.UserName
    //                };
    //                _db.ItemInstances.Add(newInstance);
    //                await _db.SaveChangesAsync(cancellationToken);

    //                _db.CurrentItemLocations.Add(new CurrentItemLocation
    //                {
    //                    ItemInstanceId = newInstance.Id,
    //                    LocationType = LocationType.BinLocation,
    //                    WarehouseId = warehouse.Id,
    //                    BinLocationId = actualBin?.Id,
    //                    ReferenceDocumentType = nameof(InventoryCheckDocument),
    //                    ReferenceDocumentId = document.Id,
    //                    ReferenceDocumentNo = document.DocumentNo,
    //                    UpdatedLocationAt = now,
    //                    UpdatedLocationBy = user.UserName,
    //                    CreatedAt = now,
    //                    CreatedBy = user.UserName
    //                });
    //                await ApplyStockDeltaAsync(warehouse.Id, actualBin?.Id, item.Id, ItemStatus.Normal, 1, user, cancellationToken);

    //                _db.InventoryCheckLines.Add(new InventoryCheckLine
    //                {
    //                    InventoryCheckDocumentId = document.Id,
    //                    ItemInstanceId = newInstance.Id,
    //                    SystemBinLocationId = null,
    //                    ActualBinLocationId = actualBin?.Id,
    //                    Result = InventoryCheckLineResult.Extra,
    //                    Note = Value(row, "Note") ?? $"Extra item found at {actualBin?.BinCode}.",
    //                    CreatedAt = now,
    //                    CreatedBy = user.UserName
    //                });
    //                //result = InventoryCheckLineResult.Extra;
    //                //_db.InventoryCheckLines.Add(new InventoryCheckLine { InventoryCheckDocumentId = document.Id, ItemInstanceId = instance?.Id, SystemBinLocationId = current?.BinLocationId, ActualBinLocationId = actualBin?.Id, Result = result, Note = Value(row, "Note"), CreatedBy = user.UserName });
    //            }
    //            else if (current == null)
    //            {
    //                continue;
    //                //result = InventoryCheckLineResult.Missing;
    //                //_db.InventoryCheckLines.Add(new InventoryCheckLine { InventoryCheckDocumentId = document.Id, ItemInstanceId = instance?.Id, SystemBinLocationId = current?.BinLocationId, ActualBinLocationId = actualBin?.Id, Result = result, Note = Value(row, "Note"), CreatedBy = user.UserName });
    //            }
    //            else if (current.BinLocationId == actualBin?.Id)
    //            {
    //                if (instance.Status != ItemStatus.InStock && instance.Status != ItemStatus.Normal)
    //                    instance.Status = ItemStatus.Normal;
    //                result = InventoryCheckLineResult.Matched;
    //                _db.InventoryCheckLines.Add(new InventoryCheckLine { InventoryCheckDocumentId = document.Id, ItemInstanceId = instance?.Id, SystemBinLocationId = current?.BinLocationId, ActualBinLocationId = actualBin?.Id, Result = result, Note = Value(row, "Note"), CreatedBy = user.UserName });
    //            }

    //            else
    //            {
    //                var oldBinId = current?.BinLocationId;
    //                _db.InventoryCheckLines.Add(new InventoryCheckLine
    //                {
    //                    InventoryCheckDocumentId = document.Id,
    //                    ItemInstanceId = instance.Id,
    //                    SystemBinLocationId = current?.BinLocationId,
    //                    ActualBinLocationId = actualBin?.Id,
    //                    Result = InventoryCheckLineResult.WrongLocation,
    //                    Note = Value(row, "Note") ?? $"Found at {actualBin?.BinCode} instead of expected location.",
    //                    CreatedAt = now,
    //                    CreatedBy = user.UserName
    //                });

    //                if (current != null)
    //                {
    //                    var fromWarehouseId = current.WarehouseId;
    //                    if (oldBinId.HasValue && fromWarehouseId.HasValue)
    //                        await ApplyStockDeltaAsync(fromWarehouseId.Value, oldBinId, instance.ItemId, instance.Status, -1, user, cancellationToken);

    //                    current.LocationType = LocationType.BinLocation;
    //                    current.WarehouseId = warehouse.Id;
    //                    current.BinLocationId = actualBin?.Id;
    //                    current.ReferenceDocumentType = nameof(InventoryCheckDocument);
    //                    current.ReferenceDocumentId = document.Id;
    //                    current.ReferenceDocumentNo = document.DocumentNo;
    //                    current.UpdatedLocationAt = now;
    //                    current.UpdatedLocationBy = user.UserName;

    //                    var bin = await FindBinIdAsync(warehouse.Id, oldBinId, cancellationToken);

    //                    await ApplyStockDeltaAsync(warehouse.Id, actualBin.Id, instance.ItemId, instance.Status, 1, user, cancellationToken);
    //                    AddHistory(instance.Id, MovementActionType.MoveLocation, LocationType.BinLocation, oldBinId, $"Bin {bin?.FullPath}", LocationType.BinLocation, actualBin.Id, actualBin.FullPath,
    //                        instance.Status, instance.Status, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, "Inventory check: wrong location corrected", user);
    //                    //AddHistory(instance.Id, MovementActionType.MoveLocation, oldBinId, $"Bin {bin?.FullPath}", LocationType.BinLocation, actualBin.Id, actualBin.FullPath, instance.Status, instance.Status, nameof(InventoryCheckDocument), document.Id, document.DocumentNo, "Inventory check: wrong location corrected", user);
    //                    //    result = InventoryCheckLineResult.WrongLocation;
    //                    //_db.InventoryCheckLines.Add(new InventoryCheckLine { InventoryCheckDocumentId = document.Id, ItemInstanceId = instance?.Id, SystemBinLocationId = current?.BinLocationId, ActualBinLocationId = actualBin?.Id, Result = result, Note = Value(row, "Note"), CreatedBy = user.UserName });
    //                }
    //            }
    //            count++;
    //        }
    //        listDocument.Add(document);


    //    }

    //    await _db.SaveChangesAsync(cancellationToken);

    //    foreach (var doc in listDocument)
    //    {
    //        var listCheckedItemInstanceIds = await _db.InventoryCheckLines.Where(x => x.InventoryCheckDocumentId == doc.Id).Select(x => x.ItemInstanceId).ToListAsync(cancellationToken);
    //        var listallInStockLocations = await _db.CurrentItemLocations.Where(x => x.WarehouseId == doc.WarehouseId && x.LocationType == LocationType.BinLocation && !listCheckedItemInstanceIds.Contains(x.ItemInstanceId)
    //        && (x.ItemInstance.Status == ItemStatus.InStock || x.ItemInstance.Status == ItemStatus.Normal || x.ItemInstance.Status == ItemStatus.Damaged || x.ItemInstance.Status == ItemStatus.Scrapped)).ToListAsync(cancellationToken);
    //        foreach (var missingLoc in listallInStockLocations)
    //        {
    //            var missingInstance = missingLoc.ItemInstance!;

    //            _db.InventoryCheckLines.Add(new InventoryCheckLine
    //            {
    //                InventoryCheckDocumentId = doc.Id,
    //                ItemInstanceId = missingInstance.Id,
    //                SystemBinLocationId = missingLoc.BinLocationId,
    //                ActualBinLocationId = null,
    //                Result = InventoryCheckLineResult.Missing,
    //                Note = "Item not found during inventory check — marked Lost.",
    //                CreatedAt = now,
    //                CreatedBy = user.UserName
    //            });

    //            var oldBinId = missingLoc.BinLocationId;
    //            var oldWarehouseId = missingLoc.WarehouseId;
    //            if (oldBinId.HasValue && oldWarehouseId.HasValue)
    //                await ApplyStockDeltaAsync(oldWarehouseId.Value, oldBinId, missingInstance.ItemId, missingInstance.Status, -1, user, cancellationToken);

    //            missingLoc.BinLocationId = null;
    //            missingLoc.ReferenceDocumentType = nameof(InventoryCheckDocument);
    //            missingLoc.ReferenceDocumentId = doc.Id;
    //            missingLoc.ReferenceDocumentNo = doc.DocumentNo;
    //            missingLoc.UpdatedLocationAt = now;
    //            missingLoc.UpdatedLocationBy = user.UserName;

    //            if (missingInstance.Status == ItemStatus.Normal || missingInstance.Status == ItemStatus.InStock) missingInstance.Status = ItemStatus.Lost;
    //            var bin = await FindBinIdAsync(doc.WarehouseId, oldBinId, cancellationToken);

    //            AddHistory(missingInstance.Id, MovementActionType.InventoryCheck, LocationType.BinLocation, oldBinId, $"Bin {bin?.FullPath}", null, null, "Unknown", ItemStatus.InStock, ItemStatus.Lost, nameof(InventoryCheckDocument), doc.Id, doc.DocumentNo, "Missing: not found during inventory check", user);
    //        }
            
    //    }

    //    await _db.SaveChangesAsync(cancellationToken);
    //    return count;
    //}
    private async Task<int> ConfirmInventoryCheckAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => Value(x, "WarehouseCode")))
        {
            var warehouse = await FindWarehouseAsync(group.Key, cancellationToken) ?? throw new InvalidOperationException("Warehouse not found.");

            var sessionDate = DateTime.UtcNow;
            var session = await _inventoryCheckService.CreateSessionAsync(new InventoryCheckSessionRequest
            {
                WarehouseId = warehouse.Id,
                WarehouseCode = warehouse.WarehouseCode,
                SessionDate = sessionDate,
                DocumentPeriodType = DocumentPeriodType.Month,
                CountMethod = "Excel",
                ResponsibleStaff = user.UserName,
                Note = NullIfEmpty(Value(group.First(), "Note"))
            }, user, cancellationToken);

            if (!session.Success || session.Data == null)
            {
                throw new InvalidOperationException(session.Message);
            }

            var scanLines = group.Select(row => new InventoryCheckLineRequest
            {
                ItemCode = Value(row, "ItemCode"),
                SerialNumber = Value(row, "SerialNumber"),
                BinCode = NullIfEmpty(Value(row, "BinCode")) ?? NullIfEmpty(Value(row, "ActualBinCode")) ?? string.Empty,
                Note = NullIfEmpty(Value(row, "Note"))
            }).ToArray();

            var scan = await _inventoryCheckService.ScanBatchAsync(new InventoryCheckScanRequest
            {
                DocumentId = session.Data.DocumentId,
                Lines = scanLines
            }, user, cancellationToken);

            if (!scan.Success)
            {
                throw new InvalidOperationException(scan.Message);
            }

            var finalize = await _inventoryCheckService.FinalizeAsync(session.Data.DocumentId, user, cancellationToken);
            if (!finalize.Success)
            {
                throw new InvalidOperationException(finalize.Message);
            }

            count += scanLines.Length;
        }
        return count;
    }

    private async Task<InventoryCheckImportContext> BuildImportContextAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CancellationToken cancellationToken)
    {
        var warehouseCodes = rows
            .Select(x => Value(x, "WarehouseCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var itemCodes = rows
            .Select(x => Value(x, "ItemCode"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var serials = rows
            .Select(x => Value(x, "SerialNumber"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var warehouses = await _db.Warehouses
            .Where(x => warehouseCodes.Contains(x.WarehouseCode))
            .ToDictionaryAsync(x => x.WarehouseCode, cancellationToken);

        var items = await _db.Items
            .Where(x => itemCodes.Contains(x.ItemCode))
            .ToDictionaryAsync(x => x.ItemCode, cancellationToken);

        var instances = await _db.ItemInstances
            .Where(x => serials.Contains(x.SerialNumber) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.SerialNumber!, cancellationToken);

        var instanceIds = instances.Values
            .Select(x => x.Id)
            .ToArray();

        var locations = await _db.CurrentItemLocations
            .Include(x => x.ItemInstance)
            .Where(x => instanceIds.Contains(x.ItemInstanceId) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.ItemInstanceId, cancellationToken);

        var warehouseIds = warehouses.Values
            .Select(x => x.Id)
            .ToArray();

        var bins = await _db.BinLocations
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .ToDictionaryAsync(
                x => (x.WarehouseId, x.BinCode),
                cancellationToken);

        return new InventoryCheckImportContext(warehouses, items, instances, locations, bins);
    }
    private async Task ProcessExtraAsync( InventoryCheckDocument document, Warehouse warehouse, BinLocation actualBin, Item item, string serial, string? note, DateTime now, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var instance = new ItemInstance
        {
            ItemId = item.Id,
            SerialNumber = serial,
            Barcode = serial,
            Status = ItemStatus.Normal,
            CreatedAt = now,
            CreatedBy = user.UserName
        };

        _db.ItemInstances.Add(instance);

        await _db.SaveChangesAsync(cancellationToken);

        _db.CurrentItemLocations.Add(new CurrentItemLocation
        {
            ItemInstance = instance,
            LocationType = LocationType.BinLocation,
            WarehouseId = warehouse.Id,
            BinLocationId = actualBin.Id,
            ReferenceDocumentType = nameof(InventoryCheckDocument),
            ReferenceDocumentId = document.Id,
            ReferenceDocumentNo = document.DocumentNo,
            UpdatedLocationAt = now,
            UpdatedLocationBy = user.UserName,
            CreatedAt = now,
            CreatedBy = user.UserName
        });

        await ApplyStockDeltaAsync(
            warehouse.Id,
            actualBin.Id,
            item.Id,
            ItemStatus.Normal,
            1,
            user,
            cancellationToken);

        _db.InventoryCheckLines.Add(new InventoryCheckLine
        {
            InventoryCheckDocumentId = document.Id,
            ItemInstance = instance,
            ActualBinLocationId = actualBin.Id,
            Result = InventoryCheckLineResult.Extra,
            Note = note,
            CreatedAt = now,
            CreatedBy = user.UserName
        });
    }
    private void ProcessMatched( InventoryCheckDocument document,ItemInstance instance, CurrentItemLocation current, BinLocation actualBin, string? note, DateTime now,CurrentUserContext user)
    {
        if (instance.Status != ItemStatus.Normal &&
            instance.Status != ItemStatus.InStock)
        {
            instance.Status = ItemStatus.Normal;
        }

        _db.InventoryCheckLines.Add(new InventoryCheckLine
        {
            InventoryCheckDocumentId = document.Id,
            ItemInstanceId = instance.Id,
            SystemBinLocationId = current.BinLocationId,
            ActualBinLocationId = actualBin.Id,
            Result = InventoryCheckLineResult.Matched,
            Note = note,
            CreatedAt = now,
            CreatedBy = user.UserName
        });
    }

    private async Task ProcessWrongLocationAsync(
    InventoryCheckDocument document,
    Warehouse warehouse,
    ItemInstance instance,
    CurrentItemLocation current,
    BinLocation actualBin,
    DateTime now,
    CurrentUserContext user,
    CancellationToken cancellationToken)
    {
        var oldBinId = current.BinLocationId;

        if (oldBinId.HasValue && current.WarehouseId.HasValue)
        {
            await ApplyStockDeltaAsync(
                current.WarehouseId.Value,
                oldBinId,
                instance.ItemId,
                instance.Status,
                -1,
                user,
                cancellationToken);
        }

        current.BinLocationId = actualBin.Id;
        current.WarehouseId = warehouse.Id;
        current.UpdatedLocationAt = now;
        current.UpdatedLocationBy = user.UserName;

        await ApplyStockDeltaAsync(
            warehouse.Id,
            actualBin.Id,
            instance.ItemId,
            instance.Status,
            1,
            user,
            cancellationToken);

        _db.InventoryCheckLines.Add(new InventoryCheckLine
        {
            InventoryCheckDocumentId = document.Id,
            ItemInstanceId = instance.Id,
            SystemBinLocationId = oldBinId,
            ActualBinLocationId = actualBin.Id,
            Result = InventoryCheckLineResult.WrongLocation,
            CreatedAt = now,
            CreatedBy = user.UserName
        });
    }

    private async Task ProcessMissingAsync(
    InventoryCheckDocument document,
    CurrentItemLocation location,
    DateTime now,
    CurrentUserContext user,
    CancellationToken cancellationToken)
{
    var instance = location.ItemInstance!;

    _db.InventoryCheckLines.Add(new InventoryCheckLine
    {
        InventoryCheckDocumentId = document.Id,
        ItemInstanceId = instance.Id,
        SystemBinLocationId = location.BinLocationId,
        Result = InventoryCheckLineResult.Missing,
        Note = "Item not found during inventory check — marked Lost.",
        CreatedAt = now,
        CreatedBy = user.UserName
    });

    if (location.BinLocationId.HasValue &&
        location.WarehouseId.HasValue)
    {
        await ApplyStockDeltaAsync(
            location.WarehouseId.Value,
            location.BinLocationId,
            instance.ItemId,
            instance.Status,
            -1,
            user,
            cancellationToken);
    }

    location.BinLocationId = null;

    if (instance.Status is ItemStatus.Normal or ItemStatus.InStock)
    {
        instance.Status = ItemStatus.Lost;
    }
}

    private async Task<int> ConfirmBorrowLendAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => NullIfEmpty(Value(x, "DocumentNo")), StringComparer.OrdinalIgnoreCase))
        {
            var firstRow = group.First();
            var documentNo = group.Key ?? _documentNumbers.Next("BRW", DateTime.UtcNow);
            var warehouse = await FindWarehouseAsync(Value(firstRow, "WarehouseCode"), cancellationToken)
                ?? throw new InvalidOperationException("Warehouse not found.");
            var borrowerCode = Value(firstRow, "BorrowerCode");
            var borrowerName = NullIfEmpty(Value(firstRow, "BorrowerName")) ?? borrowerCode;
            var borrowDate = DateTime.TryParse(Value(firstRow, "BorrowDate"), out var parsedBorrowDate) && parsedBorrowDate != default
                ? parsedBorrowDate
                : DateTime.UtcNow;
            var dueDate = DateTime.TryParse(Value(firstRow, "DueDate"), out var parsedDueDate) && parsedDueDate != default
                ? parsedDueDate
                : borrowDate.AddDays(30);

            var mixedWarehouse = group
                .Select(x => Value(x, "WarehouseCode"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any();
            if (mixedWarehouse)
            {
                throw new InvalidOperationException($"Borrow document {documentNo} contains multiple warehouses.");
            }

            var request = new BorrowLendRequest
            {
                DocumentNo = documentNo,
                WarehouseId = warehouse.Id,
                WarehouseCode = warehouse.WarehouseCode,
                Borrower = borrowerCode,
                BorrowerCode = borrowerCode,
                BorrowerName = borrowerName,
                BorrowDate = borrowDate,
                DueDate = dueDate,
                Purpose = Value(firstRow, "Purpose"),
                BorrowDepartment = Value(firstRow, "BorrowDepartment"),
                ApprovedBy = user.UserName,
                BorrowerPhone = Value(firstRow, "BorrowerPhone"),
                DepartmentOwner = Value(firstRow, "DepartmentOwner"),
                Lines = group.Select(row => new BorrowLendLineRequest
                {
                    ItemCode = Value(row, "ItemCode"),
                    SerialNumber = Value(row, "SerialNumber"),
                    TargetExternalLocation = NullIfEmpty(Value(row, "TargetExternalLocation")),
                    Note = NullIfEmpty(Value(row, "Note"))
                }).ToArray()
            };

            var result = await _borrowService.LendAsync(request, user, cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"BorrowLend failed: {string.Join("; ", result.Errors)}");
            }

            count += request.Lines.Count;
        }

        return count;
    }

    private async Task<int> ConfirmBorrowLendBulkLegacyAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => Value(x, "DocumentNo")))
        {
            var firstRow = group.First();
            var warehouseCode = Value(firstRow, "WarehouseCode");
            var warehouse = await FindWarehouseAsync(warehouseCode, cancellationToken) ?? throw new InvalidOperationException("Warehouse not found.");
            var borrowerCode = Value(firstRow, "BorrowerCode");
            var borrower = await _db.ExternalParties.FirstOrDefaultAsync(x => x.PartyCode == borrowerCode && x.PartyType == ExternalPartyType.Borrower && x.IsActive, cancellationToken);

            var docNo = NullIfEmpty(group.Key) ?? _documentNumbers.Next("BRW", DateTime.UtcNow);
            DateTime.TryParse(Value(firstRow, "BorrowDate"), out var borrowDate);
            DateTime.TryParse(Value(firstRow, "DueDate"), out var dueDate);

            var document = new BorrowDocument
            {
                DocumentNo = docNo,
                DocumentDate = borrowDate == default ? DateTime.UtcNow : borrowDate,
                BorrowerId = borrower?.Id ?? 0,
                DueDate = dueDate == default ? DateTime.UtcNow.AddDays(30) : dueDate,
                Purpose = Value(firstRow, "Purpose"),
                BorrowDepartment = Value(firstRow, "BorrowDepartment"),
                ApprovedBy = user.UserName,
                BorrowerPhone = Value(firstRow, "BorrowerPhone"),
                DepartmentOwner = Value(firstRow, "DepartmentOwner"),
                CreatedBy = user.UserName,
                ApprovedAt = borrowDate,
                PostedAt = borrowDate
            };
            _db.BorrowDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var row in group)
            {
                var lifecycleBatchId = Guid.NewGuid();
                var instance = await FindInstanceAsync(row, cancellationToken) ?? throw new InvalidOperationException("Item instance not found.");
                var current = await _db.CurrentItemLocations.FirstAsync(x => x.ItemInstanceId == instance.Id && !x.IsDeleted, cancellationToken);
                var oldStatus = instance.Status;
                var fromDisplay = current.FromDisplay();
                var targetExt = Value(row, "TargetExternalLocation").Trim();

                _db.BorrowDocumentLines.Add(new BorrowDocumentLine
                {
                    BorrowDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    FromBinLocationId = current.BinLocationId,
                    TargetExternalLocation = targetExt,
                    CreatedBy = user.UserName
                });

                instance.Status = ItemStatus.LentOut;
                current.LocationType = LocationType.Borrower;
                current.ExternalPartyId = borrower?.Id;
                current.BinLocationId = null;
                current.ExternalLocationText = targetExt;
                current.ReferenceDocumentType = nameof(BorrowDocument);
                current.ReferenceDocumentId = document.Id;
                current.ReferenceDocumentNo = document.DocumentNo;
                current.UpdatedLocationAt = DateTime.Parse(Value(row, "BorrowDate"));
                current.UpdatedLocationBy = user.UserName;


                _db.BorrowDocumentLogs.Add(new BorrowDocumentLog
                {
                    BorrowDocumentId = document.Id,
                    ItemInstanceId = instance.Id,
                    Action = "BorrowIssue",
                    OldStatus = oldStatus.ToString(),
                    NewStatus = ItemStatus.LentOut.ToString(),
                    Borrower = $"{borrower.PartyCode}-{borrower.Name}",
                    BorrowDepartment = Value(row, "BorrowDepartment"),
                    BorrowerPhone = Value(row, "BorrowerPhone"),
                    DepartmentOwner = Value(row, "DepartmentOwner"),
                    OldLocationText = fromDisplay,
                    NewLocationText = $"{borrowerCode} - {targetExt}",
                    PerformedBy = user.UserName,
                    Timestamp = DateTime.Parse(Value(row, "BorrowDate")),
                    Note = "Excel import"
                });

                AddHistory(instance.Id, MovementActionType.Lend, LocationType.BinLocation, current.BinLocationId, fromDisplay, LocationType.Borrower, borrower.Id,
                    $"{borrowerCode} - {targetExt}", oldStatus, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, "", user, lifecycleBatchId);
                //AddHistory(instance.Id, MovementActionType.Lend, fromDisplay, $"{borrowerCode} - {targetExt}", oldStatus, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, user);
                AddTransaction(InventoryTransactionType.BorrowLend, instance.ItemId, instance.Id, current.WarehouseId, null, -1, ItemStatus.LentOut, nameof(BorrowDocument), document.Id, document.DocumentNo, user, lifecycleBatchId);
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ConfirmRepairSendAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => NullIfEmpty(Value(x, "DocumentNo")), StringComparer.OrdinalIgnoreCase))
        {
            var firstRow = group.First();
            var documentNo = group.Key ?? _documentNumbers.Next("REP", DateTime.UtcNow);
            var sendDate = DateTime.UtcNow;
            var expectedReturnDate = DateTime.TryParse(Value(firstRow, "ExpectedReturnDate"), out var parsedExpectedReturnDate)
                ? parsedExpectedReturnDate
                : (DateTime?)null;

            var request = new RepairSendRequest
            {
                DocumentNo = documentNo,
                RepairSenderCode = Value(firstRow, "RepairVendorCode"),
                RepairSenderName = Value(firstRow, "RepairVendorName"),
                SendDate = sendDate,
                ExpectedReturnDate = expectedReturnDate,
                Reason = Value(firstRow, "Reason"),
                Lines = group.Select(row => new RepairSendLineRequest
                {
                    ItemCode = Value(row, "ItemCode"),
                    SerialNumber = Value(row, "SerialNumber"),
                    TargetExternalLocation = NullIfEmpty(Value(row, "TargetExternalLocation")),
                    Note = NullIfEmpty(Value(row, "Note"))
                }).ToArray()
            };

            var result = await _repairService.SendToRepairAsync(request, user, cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"RepairSend failed: {string.Join("; ", result.Errors)}");
            }

            count += request.Lines.Count;
        }

        return count;
    }

    private async Task<int> ConfirmRepairSendBulkLegacyAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        // Group by DocumentNo — supports find-or-create append (Phase 1 pattern)
        foreach (var group in rows.GroupBy(x => NullIfEmpty(Value(x, "DocumentNo"))))
        {
            var firstRow = group.First();
            var vendorCode = Value(firstRow, "RepairVendorCode").Trim().ToUpperInvariant();
            var vendor = await _db.ExternalParties.FirstAsync(x => x.PartyCode == vendorCode && x.PartyType == ExternalPartyType.RepairVendor, cancellationToken);
            if (vendor == null)
            {
                vendor = new ExternalParty
                {
                    PartyCode = vendorCode,
                    ContactName = Value(firstRow, "RepairVendorName"),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = user.UserName,
                    Email = "",
                    IsActive = true,
                    Phone = "",
                    PartyType = ExternalPartyType.RepairVendor,
                    Name = Value(firstRow, "RepairVendorName"),
                };
                _db.ExternalParties.Add(vendor);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Find-or-create document
            RepairDocument document;
            if (group.Key != null)
            {
                document = await _db.RepairDocuments.FirstOrDefaultAsync(x => x.DocumentNo == group.Key, cancellationToken)
                           ?? new RepairDocument();
                if (document.Id == 0)
                {
                    document.DocumentNo  = group.Key;
                    document.DocumentDate = DateTime.UtcNow;
                    document.RepairVendorId = vendor.Id;
                    document.Reason         = Value(firstRow, "Reason");
                    document.CreatedBy      = user.UserName;
                    document.ApprovedBy     = user.UserName;
                    document.ApprovedAt     = DateTime.UtcNow;
                    document.PostedAt       = DateTime.UtcNow;
                    _db.RepairDocuments.Add(document);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                document = new RepairDocument { DocumentNo = _documentNumbers.Next("REP", DateTime.UtcNow), DocumentDate = DateTime.UtcNow, RepairVendorId = vendor.Id, Reason = Value(firstRow, "Reason"), CreatedBy = user.UserName, ApprovedBy = user.UserName, ApprovedAt = DateTime.UtcNow, PostedAt = DateTime.UtcNow };
                _db.RepairDocuments.Add(document);
                await _db.SaveChangesAsync(cancellationToken);
            }

            foreach (var row in group)
            {
                var lifecycleBatchId = Guid.NewGuid();
                var instance = await FindInstanceAsync(row, cancellationToken) ?? throw new InvalidOperationException("Item instance not found.");
                var current = await _db.CurrentItemLocations.FirstAsync(x => x.ItemInstanceId == instance.Id && !x.IsDeleted, cancellationToken);
                var oldStatus = instance.Status;
                var fromWarehouseId = current.WarehouseId;
                var fromBinLocationId = current.BinLocationId;
                var fromDisplay = current.FromDisplay();
                var targetExternalLocation = Value(row, "TargetExternalLocation").Trim();
                _db.RepairDocumentLines.Add(new RepairDocumentLine { RepairDocumentId = document.Id, ItemInstanceId = instance.Id, FromBinLocationId = current.BinLocationId, TargetExternalLocation = targetExternalLocation, CreatedBy = user.UserName });
                instance.Status = ItemStatus.Repairing;
                current.LocationType = LocationType.RepairVendor;
                current.ExternalPartyId = vendor.Id;
                current.BinLocationId = null;
                current.ExternalLocationText = targetExternalLocation;
                current.ReferenceDocumentType = nameof(RepairDocument);
                current.ReferenceDocumentId = document.Id;
                current.ReferenceDocumentNo = document.DocumentNo;
                current.UpdatedLocationAt = DateTime.UtcNow;
                current.UpdatedLocationBy = user.UserName;
                if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                {
                    await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
                }

                AddHistory(instance.Id, MovementActionType.SendToRepair, LocationType.BinLocation, fromBinLocationId, fromDisplay, LocationType.RepairVendor, vendor.Id,
                    $"{vendor.Name} - {targetExternalLocation}", oldStatus, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, "", user, lifecycleBatchId);
                //AddHistory(instance.Id, MovementActionType.SendToRepair, fromDisplay, $"{vendor.Name} - {targetExternalLocation}", oldStatus, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user);
                AddTransaction(InventoryTransactionType.RepairSend, instance.ItemId, instance.Id, current.WarehouseId, null, -1, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user, lifecycleBatchId);
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<ItemCategory> EnsureCategoryAsync(string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var category = await _db.ItemCategories.FirstOrDefaultAsync(x => x.CategoryCode == code, cancellationToken);
        if (category != null) return category;
        category = new ItemCategory { CategoryCode = code, Name = name, CreatedBy = user.UserName };
        _db.ItemCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category;
    }

    private async Task<ItemUnit> EnsureUnitAsync(string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var unit = await _db.ItemUnits.FirstOrDefaultAsync(x => x.UnitCode == code, cancellationToken);
        if (unit != null) return unit;
        unit = new ItemUnit { UnitCode = code, Name = name, CreatedBy = user.UserName };
        _db.ItemUnits.Add(unit);
        await _db.SaveChangesAsync(cancellationToken);
        return unit;
    }

    private async Task<Company> EnsureCompanyAsync(string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.Companies.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (entity != null) return entity;
        entity = new Company { Code = code, Name = name, CreatedBy = user.UserName };
        _db.Companies.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<Branch> EnsureBranchAsync(int companyId, string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.Branches.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);
        if (entity != null) return entity;
        entity = new Branch { CompanyId = companyId, Code = code, Name = name, CreatedBy = user.UserName };
        _db.Branches.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<Warehouse> EnsureWarehouseAsync(int branchId, string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == code, cancellationToken);
        if (entity != null) return entity;
        entity = new Warehouse { BranchId = branchId, WarehouseCode = code, Name = name, CreatedBy = user.UserName };
        _db.Warehouses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<WarehouseZone> EnsureZoneAsync(int warehouseId, string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.WarehouseZones.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ZoneCode == code, cancellationToken);
        if (entity != null) return entity;
        entity = new WarehouseZone { WarehouseId = warehouseId, ZoneCode = code, Name = name, CreatedBy = user.UserName };
        _db.WarehouseZones.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<Rack> EnsureRackAsync(int zoneId, string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.Racks.FirstOrDefaultAsync(x => x.WarehouseZoneId == zoneId && x.RackCode == code, cancellationToken);
        if (entity != null) return entity;
        entity = new Rack { WarehouseZoneId = zoneId, RackCode = code, Name = name, CreatedBy = user.UserName };
        _db.Racks.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<Shelf> EnsureShelfAsync(int rackId, string code, string name, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var entity = await _db.Shelves.FirstOrDefaultAsync(x => x.RackId == rackId && x.ShelfCode == code, cancellationToken);
        if (entity != null) return entity;
        entity = new Shelf { RackId = rackId, ShelfCode = code, Name = name, CreatedBy = user.UserName };
        _db.Shelves.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<Item?> FindItemAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        return await _db.Items.FirstOrDefaultAsync(x => x.ItemCode.ToUpper() == normalized && x.IsActive, cancellationToken);
    }

    private async Task<Warehouse?> FindWarehouseAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        return await _db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode.ToUpper() == normalized && x.IsActive, cancellationToken);
    }

    private async Task<BinLocation?> FindBinAsync(int warehouseId, string binCode, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(binCode);
        return await _db.BinLocations.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.BinCode.ToUpper() == normalized && x.IsActive, cancellationToken);
    }

    private async Task<BinLocation?> FindBinIdAsync(int warehouseId, int? binId, CancellationToken cancellationToken)
    {
        if (!binId.HasValue) return null;
        return await _db.BinLocations.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.Id == binId.Value && x.IsActive, cancellationToken);
    }

    private async Task<ItemInstance?> FindInstanceAsync(Dictionary<string, string> row, CancellationToken cancellationToken)
    {
        var serial = NormalizeCode(Value(row, "SerialNumber"));
        var code = NormalizeCode(Value(row, "ItemCode"));
        return await _db.ItemInstances.Include(x => x.Item).FirstOrDefaultAsync(x =>
            !x.IsDeleted &&
            (!string.IsNullOrWhiteSpace(serial) && x.SerialNumber != null && x.SerialNumber.ToUpper() == serial) &&
            (x.Item != null && x.Item.ItemCode == code), cancellationToken);
    }
    private Task<Item?> FindItemByCodeAsync(string? itemCode, CancellationToken ct)
        => string.IsNullOrWhiteSpace(itemCode) ? Task.FromResult<Item?>(null)
            : _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.ItemCode == itemCode.Trim() && x.IsActive, ct);

    private async Task ApplyStockDeltaAsync(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var balance = await _db.StockBalances.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status, cancellationToken);
        if (balance == null)
        {
            balance = new StockBalance { WarehouseId = warehouseId, BinLocationId = binLocationId, ItemId = itemId, Status = status, Quantity = 0, CreatedBy = user.UserName };
            _db.StockBalances.Add(balance);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = DateTime.UtcNow;
        balance.UpdatedBy = user.UserName;
    }

    //private void AddHistory(int itemInstanceId, MovementActionType action, string? from, string? to, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, CurrentUserContext user)
    //{
    //    _db.ItemMovementHistories.Add(new ItemMovementHistory { ItemInstanceId = itemInstanceId, ActionType = action, FromLocationDisplay = from, ToLocationDisplay = to, OldStatus = oldStatus, NewStatus = newStatus, DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo, PerformedAt = DateTime.UtcNow, PerformedBy = user.UserName });

    private void AddHistory(int itemInstanceId, MovementActionType action, LocationType? fromType, int? fromId, string? fromDisplay, LocationType? toType, int? toId, string? toDisplay, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, string? note, CurrentUserContext user, Guid? lifecycleBatchId = null)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory
        {
            ItemInstanceId = itemInstanceId, ActionType = action,
            FromLocationType = fromType, FromLocationId = fromId, FromLocationDisplay = fromDisplay,
            ToLocationType = toType, ToLocationId = toId, ToLocationDisplay = toDisplay,
            OldStatus = oldStatus, NewStatus = newStatus,
            DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo,
            LifecycleBatchId = lifecycleBatchId,
            Note = note, PerformedAt = DateTime.UtcNow, PerformedBy = user.UserName
        });
    }

    //private void AddTransaction(InventoryTransactionType type, int itemId, int? itemInstanceId, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user)
    //{
    //    _db.InventoryTransactions.Add(new InventoryTransaction { TransactionType = type, ItemId = itemId, ItemInstanceId = itemInstanceId, WarehouseId = warehouseId, BinLocationId = binLocationId, QuantityDelta = quantityDelta, StatusAfter = statusAfter, DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo, PostedAt = DateTime.UtcNow, PostedBy = user.UserName });
    //}
    private void AddTransaction(InventoryTransactionType type, int itemId, int? itemInstanceId, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user, Guid? lifecycleBatchId = null)
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
            LifecycleBatchId = lifecycleBatchId,
            PostedAt = DateTime.UtcNow,
            PostedBy = user.UserName
        });
    }

    private static Dictionary<string, string> Row(ImportBatchRow row)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawJson, JsonOptions) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Value(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private async Task<Dictionary<(int ItemId, string SnCode), string>> LoadQuantityOwnerNamesAsync(
        IEnumerable<(int ItemId, string SnCode)> keys,
        CancellationToken cancellationToken)
    {
        var normalizedKeys = keys
            .Where(x => !string.IsNullOrWhiteSpace(x.SnCode))
            .Select(x => (x.ItemId, SnCode: x.SnCode!.Trim()))
            .Distinct()
            .ToArray();
        if (normalizedKeys.Length == 0)
        {
            return new Dictionary<(int ItemId, string SnCode), string>();
        }

        var itemIds = normalizedKeys.Select(x => x.ItemId).Distinct().ToArray();
        var snCodes = normalizedKeys.Select(x => x.SnCode).Distinct().ToArray();
        var candidateSet = normalizedKeys.ToHashSet();

        var owners = await _db.ItemInstances
            .AsNoTracking()
            .Where(x =>
                x.TrackingType == ItemTrackingType.QuantityOnly &&
                !x.IsDeleted &&
                x.SerialNumber != null &&
                itemIds.Contains(x.ItemId) &&
                snCodes.Contains(x.SerialNumber))
            .Select(x => new { x.ItemId, x.SerialNumber, x.OwnerName })
            .ToArrayAsync(cancellationToken);

        var result = new Dictionary<(int ItemId, string SnCode), string>();
        foreach (var owner in owners)
        {
            var key = (owner.ItemId, owner.SerialNumber!.Trim());
            if (candidateSet.Contains(key) && !result.ContainsKey(key))
            {
                result[key] = owner.OwnerName ?? string.Empty;
            }
        }

        return result;
    }

    private static string QuantityOwnerName(
        IReadOnlyDictionary<(int ItemId, string SnCode), string> ownerNames,
        int itemId,
        string? snCode)
    {
        return !string.IsNullOrWhiteSpace(snCode) && ownerNames.TryGetValue((itemId, snCode.Trim()), out var ownerName)
            ? ownerName
            : string.Empty;
    }

    private static IReadOnlyCollection<object?> OrderedBatchExportRow(string importType, ImportBatchRow batchRow, IReadOnlyCollection<string> orderedHeaders)
    {
        var row = CanonicalizeImportRow(importType, Row(batchRow));
        return orderedHeaders
            .Select(header => (object?)Value(row, header))
            .Concat(new object?[]
            {
                batchRow.RowNumber,
                batchRow.Severity.ToString(),
                batchRow.Message ?? string.Empty,
                batchRow.SuggestedFix ?? string.Empty
            })
            .ToArray();
    }

    private static string NormalizeDownloadKind(string? kind)
    {
        var normalized = (kind ?? "preview").Trim().Replace("_", "-").ToLowerInvariant();
        return normalized switch
        {
            "original" or "uploaded" => "original",
            "template" => "template",
            "errors" or "error" or "validationerrors" or "validation-errors" => "validation-errors",
            "validated" or "preview" => "preview",
            "result" or "confirmed" => "result",
            _ => "preview"
        };
    }

    private static string SafeDownloadName(string importType, string label, string extension = ".xlsx")
    {
        var safeType = new string((importType ?? "Import").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safeType)) safeType = "Import";
        if (string.IsNullOrWhiteSpace(extension)) extension = ".xlsx";
        if (!extension.StartsWith('.')) extension = "." + extension;
        return $"{safeType}_{label}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
    }

    private static string ContentTypeForFile(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => "text/csv",
            ".tsv" => "text/tab-separated-values",
            _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

    private static string NormalizeCode(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static BinLocationUsageType ParseBinUsageType(string? value)
    {
        return Enum.TryParse<BinLocationUsageType>(value, true, out var parsed)
            ? parsed
            : BinLocationUsageType.LocationTracked;
    }

    private static string[] DistinctCodes(IReadOnlyCollection<Dictionary<string, string>> rows, params string[] keys)
    {
        return rows
            .SelectMany(row => keys.Select(key => NormalizeCode(Value(row, key))))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<T[]> Batch<T>(IEnumerable<T> values, int size = 1000)
    {
        var chunk = new List<T>(size);
        foreach (var value in values)
        {
            chunk.Add(value);
            if (chunk.Count < size)
            {
                continue;
            }

            yield return chunk.ToArray();
            chunk.Clear();
        }

        if (chunk.Count > 0)
        {
            yield return chunk.ToArray();
        }
    }

    private static Item? FindPreloadedItem(ImportValidationContext context, string code)
    {
        return context.ActiveItemsByCode.TryGetValue(NormalizeCode(code), out var item) ? item : null;
    }

    private static Warehouse? FindPreloadedWarehouse(ImportValidationContext context, string code)
    {
        return context.ActiveWarehousesByCode.TryGetValue(NormalizeCode(code), out var warehouse) ? warehouse : null;
    }

    private static BinLocation? FindPreloadedBin(
        ImportValidationContext context,
        int warehouseId,
        string binCode,
        BinLocationUsageType? usageType = null)
    {
        if (!context.ActiveBinsByWarehouseAndCode.TryGetValue((warehouseId, NormalizeCode(binCode)), out var bin))
        {
            return null;
        }

        return !usageType.HasValue || bin.UsageType == usageType.Value ? bin : null;
    }

    private static ItemInstance? FindPreloadedInstance(ImportValidationContext context, Dictionary<string, string> row)
    {
        var serial = NormalizeCode(Value(row, "SerialNumber"));
        var barcode = NormalizeCode(Value(row, "Barcode"));
        var code = NormalizeCode(Value(row, "ItemCode"));
        if (!string.IsNullOrWhiteSpace(serial) &&
            !string.IsNullOrWhiteSpace(code) &&
            context.InstancesByItemAndSerial.TryGetValue((code, serial), out var instanceByItem))
        {
            return instanceByItem;
        }

        if (!string.IsNullOrWhiteSpace(serial) &&
            context.InstancesBySerialOrBarcode.TryGetValue(serial, out var instanceBySerial))
        {
            return instanceBySerial;
        }

        return !string.IsNullOrWhiteSpace(barcode) &&
               context.InstancesBySerialOrBarcode.TryGetValue(barcode, out var instanceByBarcode)
            ? instanceByBarcode
            : null;
    }

    private static bool HasPreloadedInstance(ImportValidationContext context, string itemCode, string serialNumber)
    {
        return context.InstancesByItemAndSerial.ContainsKey((NormalizeCode(itemCode), NormalizeCode(serialNumber)));
    }

    private static string NormalizeHeaderKey(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
    }

    private static Dictionary<string, string> CanonicalizeImportRow(string importType, Dictionary<string, string> row)
    {
        var aliases = HeaderAliases(importType);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in row)
        {
            var normalized = NormalizeHeaderKey(pair.Key);
            var key = aliases.TryGetValue(normalized, out var canonical) ? canonical : pair.Key;
            result[key] = pair.Value?.Trim() ?? string.Empty;
        }

        return result;
    }

    private static Dictionary<string, string> HeaderAliases(string importType)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!ImportHeaders.TryGetValue(importType, out var headers))
        {
            return aliases;
        }

        foreach (var header in headers)
        {
            aliases[NormalizeHeaderKey(header)] = header;
            foreach (var language in ExcelResources.Values)
            {
                if (language.TryGetValue(header, out var localized))
                {
                    aliases[NormalizeHeaderKey(localized)] = header;
                }
            }
        }

        return aliases;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Bool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static bool TryParseBool(string value, out bool result)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            value == "1")
        {
            result = true;
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            value == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool HasItemMasterUpdateField(Dictionary<string, string> row)
    {
        return !string.IsNullOrWhiteSpace(Value(row, "NewCategoryCode")) ||
               !string.IsNullOrWhiteSpace(Value(row, "NewUnitCode")) ||
               !string.IsNullOrWhiteSpace(Value(row, "NewDefaultName")) ||
               !string.IsNullOrWhiteSpace(Value(row, "NewOwnerName")) ||
               !string.IsNullOrWhiteSpace(Value(row, "NewIsSerialManaged"));
    }

    private static string BuildItemMasterUpdateSummary(
        Dictionary<string, string> row,
        IReadOnlyDictionary<string, Item> items,
        IReadOnlyDictionary<string, ItemCategory> categories,
        IReadOnlyDictionary<string, ItemUnit> units)
    {
        if (!items.TryGetValue(Value(row, "ItemCode"), out var item))
        {
            return "No update field specified.";
        }

        var changes = new List<string>();
        var newCategoryCode = Value(row, "NewCategoryCode");
        if (!string.IsNullOrWhiteSpace(newCategoryCode) && categories.TryGetValue(newCategoryCode, out var category))
        {
            changes.Add($"Category: {item.Category?.CategoryCode ?? "-"} -> {category.CategoryCode}");
        }

        var newUnitCode = Value(row, "NewUnitCode");
        if (!string.IsNullOrWhiteSpace(newUnitCode) && units.TryGetValue(newUnitCode, out var unit))
        {
            changes.Add($"Unit: {item.Unit?.UnitCode ?? "-"} -> {unit.UnitCode}");
        }

        var newDefaultName = Value(row, "NewDefaultName");
        if (!string.IsNullOrWhiteSpace(newDefaultName))
        {
            changes.Add($"DefaultName: {item.DefaultName} -> {newDefaultName}");
        }

        var newOwnerName = Value(row, "NewOwnerName");
        if (!string.IsNullOrWhiteSpace(newOwnerName))
        {
            changes.Add($"OwnerName: update active instances -> {newOwnerName}");
        }

        var newSerialManaged = Value(row, "NewIsSerialManaged");
        if (!string.IsNullOrWhiteSpace(newSerialManaged) && TryParseBool(newSerialManaged, out var parsed))
        {
            changes.Add($"SerialManaged: {FormatBool(item.IsSerialManaged)} -> {FormatBool(parsed)}");
        }

        return changes.Count == 0 ? "No effective change." : string.Join("; ", changes);
    }

    private static string FormatBool(bool value) => value ? "Yes" : "No";

    private static object BuildItemMasterAuditSnapshot(Item item, IEnumerable<ItemInstance> instances)
    {
        return new
        {
            item.ItemCode,
            item.DefaultName,
            CategoryCode = item.Category?.CategoryCode,
            UnitCode = item.Unit?.UnitCode,
            item.IsSerialManaged,
            OwnerNames = instances.Select(x => x.OwnerName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToArray()
        };
    }

    private static bool IsInstructionRow(Dictionary<string, string> row)
    {
        if (row.Values.All(string.IsNullOrWhiteSpace))
        {
            return true;
        }

        var firstValue = row.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
        return firstValue.StartsWith("NOTE:", StringComparison.OrdinalIgnoreCase) ||
               firstValue.StartsWith("DELETE NOTE", StringComparison.OrdinalIgnoreCase) ||
               firstValue.StartsWith("#", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<int, List<string>> BuildBatchValidationErrors(string importType, IReadOnlyCollection<(ImportBatchRow Row, Dictionary<string, string> Data)> rows)
    {
        var result = new Dictionary<int, List<string>>();
        //if (!importType.Equals("Inbound", StringComparison.OrdinalIgnoreCase))
        //{
        //    return result;
        //}
        switch (importType.ToLower())
        {
            case "inbound":
                AddDuplicateErrors("SerialNumber", "SerialNumber is duplicated in this import file.");
                AddDuplicateErrors("Barcode", "Barcode is duplicated in this import file.");
                AddDuplicateKeyErrors(
                    x => $"{Value(x.Data, "WarehouseCode")}|{Value(x.Data, "BinCode")}",
                    x => !string.IsNullOrWhiteSpace(Value(x.Data, "BinCode")),
                    "BinCode is duplicated in this import file.");
                break;
            case "itemmaster":
                AddDuplicateErrors("ItemCode", "ItemCode is duplicated in this import file.");
                break;
            case "warehousestructure":
                AddDuplicateKeyErrors(
                    x => $"{Value(x.Data, "WarehouseCode")}|{Value(x.Data, "BinCode")}",
                    x => !string.IsNullOrWhiteSpace(Value(x.Data, "BinCode")),
                    "BinCode is duplicated in this import file.");
                break;
            case "inventorycheck":
                AddDuplicateKeyErrors(
                    x => "InventoryCheckWarehouse",
                    x => rows
                        .Select(r => Value(r.Data, "WarehouseCode"))
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Skip(1)
                        .Any(),
                    "InventoryCheck import supports one warehouse per session file.");
                AddDuplicateErrors("SerialNumber", "SerialNumber is duplicated in this import file.");
                break;
            case "repairsend":
            case "borrowlend":
            case "movelocation":
            case "borrowreturn":
            case "repairreceive":
                AddDuplicateErrors("SerialNumber", "SerialNumber is duplicated in this import file.");
                break;
            case "quantityinbound":
            case "quantityoutbound":
            case "quantityadjust":
                AddDuplicateKeyErrors(
                    x => $"{Value(x.Data, "WarehouseCode")}|{Value(x.Data, "ItemCode")}|{Value(x.Data, "SnCode")}",
                    x => !string.IsNullOrWhiteSpace(Value(x.Data, "SnCode")),
                    "SnCode + ItemCode + Warehouse is duplicated in this import file.");
                break;
        }

        //AddDuplicateErrors("SerialNumber", "SerialNumber is duplicated in this import file.");
        //AddDuplicateErrors("Barcode", "Barcode is duplicated in this import file.");
        //AddDuplicateKeyErrors(x => $"{Value(x.Data, "WarehouseCode")}|{Value(x.Data, "BinCode")}", x => !string.IsNullOrWhiteSpace(Value(x.Data, "BinCode")), "BinCode is duplicated in this import file.");
        return result;

        void AddDuplicateErrors(string column, string message)
        {
            AddDuplicateKeyErrors(x => Value(x.Data, column), x => !string.IsNullOrWhiteSpace(Value(x.Data, column)), message);
        }

        void AddDuplicateKeyErrors(Func<(ImportBatchRow Row, Dictionary<string, string> Data), string> keySelector, Func<(ImportBatchRow Row, Dictionary<string, string> Data), bool> predicate, string message)
        {
            var groups = rows
                .Where(predicate)
                .Select(x => new { x.Row.Id, Value = keySelector(x) })
                .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1);

            foreach (var group in groups)
            {
                foreach (var row in group)
                {
                    if (!result.TryGetValue(row.Id, out var messages))
                    {
                        messages = new List<string>();
                        result[row.Id] = messages;
                    }

                    messages.Add(message);
                }
            }
        }
    }

    private static IReadOnlyCollection<IReadOnlyCollection<object?>> TemplateRows(string importType)
    {
        var rows = new List<IReadOnlyCollection<object?>>();
        switch (importType)
        {
            case "ItemMaster":
                rows.Add(new object?[] { "GB200", "NVIDIA GB200 GPU", "GPU", "Graphics adapters", "PCS", "Piece", "yes", "GPU GB200", "NVIDIA GB200 GPU", "GB200 GPU" });
                rows.Add(new object?[] { "GB300", "NVIDIA GB300 GPU", "GPU", "Graphics adapters", "PCS", "Piece", "yes", "GPU GB300", "NVIDIA GB300 GPU", "GB300 GPU" });
                break;
            case "ItemMasterUpdate":
                rows.Add(new object?[] { "GB200", "GPU", "CONSUMABLE", "PCS", "", "NVIDIA GB200 GPU - corrected", "IT Dept", "", "Correct category only" });
                rows.Add(new object?[] { "GB300", "", "", "PCS", "EA", "", "", "no", "Correct unit and serial flag" });
                break;
            case "WarehouseStructure":
                rows.Add(new object?[] { "FOXCON", "FOXCON", "FII", "FUYU", "B34", "B34", "F16", "F16", "R01", "Rack 01", "S01", "Shelf 01", "B34_R01_S01", "LocationTracked" });
                rows.Add(new object?[] { "FOXCON", "FOXCON", "FII", "FUYU", "B34", "B34", "F16", "F16", "R01", "Rack 01", "S02", "Shelf 02", "B34_R01_S02", "Quantity" });
                break;
            case "InventoryCheck":
                rows.Add(new object?[] { "B34", "GB200", "SN-GB200-0001", "B34_R01_S01", "Checked OK" });
                rows.Add(new object?[] { "B34", "GB300", "SN-GB300-0002", "B34_R01_S02", "Found in wrong bin" });
                break;
            case "RepairSend":
                rows.Add(new object?[] { "REP01", "REP-VENDOR", "Hoa", "SN-GB200-0001", "", "Warranty repair", "2026-05-15", "Vendor workshop shelf A" });
                rows.Add(new object?[] { "REP01", "REP-VENDOR", "Hoa", "SN-GB200-0002", "", "Failure analysis", "2026-05-20", "Vendor receiving desk" });
                break;
            case "BorrowLend":
                rows.Add(new object?[] { "BRW-PARTY", "B34", "BRW01", "2026-05-07", "2026-06-07", "Lab testing", "IT Dept", "BRW-PHONE", "IT Manager", "GB200", "SN-GB200-0001", "External lab" });
                rows.Add(new object?[] { "BRW-PARTY", "B34", "BRW01", "2026-05-07", "2026-06-07", "Lab testing", "IT Dept", "BRW-PHONE", "IT Manager", "GB300", "SN-GB300-0001", "External lab" });
                break;
            case "QuantityInbound":
                rows.Add(new object?[] { "QIN-AUTO", "2026-05-15", "B34", "CONSUMABLE", "SCREW-M3", "LOT-001", 100, "Normal", "IT Dept", "Opening quantity" });
                break;
            case "QuantityOutbound":
                rows.Add(new object?[] { "QOUT-AUTO", "2026-05-15", "B34", "CONSUMABLE", "SCREW-M3", "LOT-001", 10, "Normal", "IT Dept", "Production issue" });
                break;
            case "QuantityAdjust":
                rows.Add(new object?[] { "QADJ-AUTO", "2026-05-15", "B34", "CONSUMABLE", "SCREW-M3", "LOT-001", 90, "Normal", "IT Dept", "Cycle count adjustment" });
                break;
            case "MoveLocation":
                rows.Add(new object?[] { "2026-05-15", "SN-GB200-0001", "", "B34", "B34", "B34_R01_S02", "Move to new bin" });
                break;
            case "BorrowReturn":
                rows.Add(new object?[] { "2026-05-15", "BRW01", "SN-GB200-0001", "", "B34_R01_S01", "Returned OK" });
                break;
            case "RepairReceive":
                rows.Add(new object?[] { "2026-05-15", "REP01", "SN-GB200-0001", "", "B34", "B34_R01_S01", "InStock", "Repair completed" });
                break;
            default:
                rows.Add(new object?[] { "2026-05-15", "INB-AUTO", "GB200", "6056066260002", "6056066260002", "", "B34", "B34_R01_S01", "SUP01", "Normal", "Sample inbound row 1", "RCV01", "Receiver A", "0900000000", "IT", "IT Dept", "LocationTracked"});
                rows.Add(new object?[] { "2026-05-15", "INB-AUTO", "GB200", "6056066260006", "6056066260006", "", "B34", "B34_R01_S02", "SUP01", "Normal", "Sample inbound row 2", "RCV01", "Receiver A", "0900000000", "IT", "IT Dept", "LocationTracked"});
                break;
        }

        //rows.Add(new object?[] { $"NOTE: Column guide: {TemplateColumnGuide(importType)}" });
        //rows.Add(new object?[] { $"NOTE: Prerequisites: {TemplatePrerequisites(importType)}" });
        //rows.Add(new object?[] { "NOTE: Delete all NOTE rows before importing real data. The system will ignore NOTE rows but keeping them makes review harder." });
        //rows.Add(new object?[] { "NOTE: Required values must match system codes exactly. Enum examples: Result=Matched/Missing/Extra/WrongLocation/Damaged; IsSerialManaged=yes/no." });
        //rows.Add(new object?[] { "NOTE: Invalid values include duplicated SerialNumber, duplicated Barcode, occupied BinCode for inbound, missing required codes, or codes outside your warehouse permissions." });
        return rows;
    }

    private static string TemplateColumnGuide(string importType)
    {
        return importType switch
        {
            "ItemMaster" => "ItemCode unique SKU; DefaultName item name; CategoryCode/CategoryName group; UnitCode/UnitName unit; IsSerialManaged yes/no; NameVi/NameEn/NameZh translations.",
            "ItemMasterUpdate" => "ItemCode existing SKU; CurrentCategoryCode/CurrentUnitCode optional safety checks; NewCategoryCode/NewUnitCode/NewDefaultName/NewOwnerName/NewIsSerialManaged are selective updates.",
            "WarehouseStructure" => "Company/Branch/Warehouse identify hierarchy; Zone/Rack/Shelf identify storage levels; BinCode should follow WarehouseCode_RackCode_ShelfCode unless intentionally edited.",
            "InventoryCheck" => "WarehouseCode checked warehouse; SerialNumber or Barcode identifies item except Extra; ActualBinCode physical bin found; Result is Matched/Missing/Extra/WrongLocation/Damaged; Note explains variance.",
            "RepairSend" => "RepairVendorCode active repair vendor; SerialNumber or Barcode identifies item; Reason repair reason; ExpectedReturnDate yyyy-MM-dd; TargetExternalLocation external repair location.",
            _ => "ItemCode existing SKU; SerialNumber/Barcode unique item identifiers; WarehouseCode inbound warehouse; BinCode empty target bin; SourcePartyCode supplier; Condition item condition; Note optional."
        };
    }

    private static string TemplatePrerequisites(string importType)
    {
        return importType switch
        {
            "ItemMaster" => "None, but CategoryCode and UnitCode will be created if missing.",
            "ItemMasterUpdate" => "Items, target categories and target units must already exist. The import never creates missing items, categories or units.",
            "WarehouseStructure" => "User must have manager permission for the target warehouse or be admin.",
            "InventoryCheck" => "Warehouse, items and bins must already exist; physical count must be completed before import.",
            "RepairSend" => "Items must be InStock or Damaged; repair vendor must exist; target external repair location must be known.",
            _ => "Items, warehouse, bins and supplier codes must already exist; target bins must be empty; serial/barcode must not exist in the system."
        };
    }

    private static void AddItemTranslation(Item item, string language, string value, CurrentUserContext user)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            item.Translations.Add(new ItemTranslation { LanguageCode = language, FieldName = "DefaultName", Value = value, CreatedBy = user.UserName });
        }
    }

    private static IReadOnlyCollection<string> RequiredColumns(string importType)
    {
        return importType switch
        {
            "ItemMaster"         => new[] { "ItemCode", "DefaultName", "CategoryCode", "UnitCode" },
            "ItemMasterUpdate"   => new[] { "ItemCode" },
            "WarehouseStructure" => new[] { "CompanyCode", "BranchCode", "WarehouseCode", "ZoneCode", "RackCode", "ShelfCode", "BinCode" },
            "Inbound"            => new[] { "ItemCode", "WarehouseCode" },
            "InventoryCheck"     => new[] { "WarehouseCode" },
            "RepairSend"         => new[] { "RepairVendorCode", "Reason", "TargetExternalLocation" },
            "BorrowLend"         => new[] { "BorrowerCode", "WarehouseCode", "SerialNumber" },
            "QuantityInbound"    => new[] { "WarehouseCode", "ItemCode", "SnCode", "Quantity" },
            "QuantityOutbound"   => new[] { "WarehouseCode", "ItemCode", "SnCode", "Quantity" },
            "QuantityAdjust"     => new[] { "WarehouseCode", "ItemCode", "SnCode", "Quantity" },
            "MoveLocation"       => new[] { "TargetWarehouseCode", "TargetBinCode" },
            "BorrowReturn"       => new[] { "SerialNumber" },
            "RepairReceive"      => new[] { "SerialNumber", "TargetWarehouseCode", "TargetBinCode" },
            _                    => Array.Empty<string>()
        };
    }

    private static string NormalizeImportType(string importType)
    {
        return importType.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private static bool CanUseImportType(string importType, CurrentUserContext user)
    {
        return importType is not ("ItemMaster" or "ItemMasterUpdate" or "WarehouseStructure") || user.CanManage;
    }

    private static string[] Headers(CurrentUserContext user, params string[] keys)
    {
        return keys.Select(x => ExcelText(user, x)).ToArray();
    }

    private static string[] ImportTemplateHeaders(CurrentUserContext user, string importType)
    {
        return ImportHeaders.TryGetValue(importType, out var headers)
            ? Headers(user, headers)
            : Array.Empty<string>();
    }

    private static string ExcelText(CurrentUserContext user, string key)
    {
        var language = user.LanguageCode?.ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };

        return ExcelResources.TryGetValue(language, out var resources) && resources.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> ExcelResources = new()
    {
        ["vi"] = new()
        {
            ["Inventory"] = "Tồn kho",
            ["History"] = "Lịch sử",
            ["Audit"] = "Nhật ký",
            ["ItemCode"] = "Mã hàng",
            ["ItemName"] = "Tên hàng",
            ["SerialNumber"] = "Serial",
            ["Barcode"] = "Barcode",
            ["Status"] = "Trạng thái",
            ["Warehouse"] = "Kho",
            ["Location"] = "Vị trí",
            ["Holder"] = "Người giữ",
            ["ReferenceDocumentNo"] = "Số chứng từ tham chiếu",
            ["UpdatedAt"] = "Cập nhật lúc",
            ["UpdatedBy"] = "Cập nhật bởi",
            ["PerformedAt"] = "Thời gian",
            ["Action"] = "Hoạt động",
            ["FromLocation"] = "Từ vị trí",
            ["ToLocation"] = "Đến vị trí",
            ["OldStatus"] = "Trạng thái cũ",
            ["NewStatus"] = "Trạng thái mới",
            ["DocumentNo"] = "Số chứng từ",
            ["PerformedBy"] = "Người thực hiện",
            ["Note"] = "Ghi chú",
            ["CreatedAt"] = "Tạo lúc",
            ["UserName"] = "Tài khoản",
            ["EntityName"] = "Đối tượng",
            ["EntityId"] = "ID đối tượng",
            ["ReferenceNo"] = "Số tham chiếu",
            ["Result"] = "Kết quả",
           
            ["Dashboard"] = "Bảng điều khiển",
            ["Tracking"] = "Tra cứu hàng",
            ["Inventory List"] = "Danh sách tồn kho",
            ["Inbound Create"] = "Nhập kho",
            ["Move Location"] = "Chuyển vị trí",
            ["Adjustment"] = "Điều chỉnh",
            ["Inventory Check"] = "Kiểm kê",
            ["Repair Send"] = "Gửi sửa chữa",
            ["Repair Receive"] = "Nhận sửa chữa",
            ["Borrow Lend"] = "Cho mượn",
            ["Borrow Return"] = "Nhận trả",
            ["Warehouse Structure"] = "Cấu trúc kho",
            ["Master Data"] = "Dữ liệu danh mục",
            ["Import Excel"] = "Nhập Excel",
            ["Reports / Audit"] = "Báo cáo / Nhật ký",
            ["System"] = "Hệ thống",
            ["Search"] = "Tìm kiếm",
            ["Warehouse"] = "Kho",
            ["Status"] = "Trạng thái",
            ["Category"] = "Nhóm hàng",
            ["Keyword"] = "Từ khóa",
            ["Current Location"] = "Vị trí hiện tại",
            ["Timeline"] = "Lịch sử",
            ["Related Documents"] = "Chứng từ liên quan",
            ["No data"] = "Không có dữ liệu",

            ["Enum.ItemStatus.InStock"] = "Trong kho",
            ["Enum.ItemStatus.Normal"] = "Bình thường",
            ["Enum.ItemStatus.Reserved"] = "Đã giữ chỗ",
            ["Enum.ItemStatus.Repairing"] = "Đang sửa chữa",
            ["Enum.ItemStatus.LentOut"] = "Đã cho mượn",
            ["Enum.ItemStatus.Returned"] = "Đã trả",
            ["Enum.ItemStatus.Damaged"] = "Hư hỏng",
            ["Enum.ItemStatus.Lost"] = "Thất lạc",
            ["Enum.ItemStatus.Disposed"] = "Đã thanh lý",
            ["Enum.ItemStatus.InTransit"] = "Đang vận chuyển",
            ["Enum.ItemStatus.Replacement"] = "Thay thế serial",
            ["Enum.ItemStatus.Scrapped"] = "Báo phế",

            ["InStock"] = "Trong kho",
            ["Reserved"] = "Đã giữ chỗ",
            ["Repairing"] = "Đang sửa chữa",
            ["LentOut"] = "Đã cho mượn",
            ["Returned"] = "Đã trả",
            ["Damaged"] = "Hư hỏng",
            ["Lost"] = "Thất lạc",
            ["Disposed"] = "Đã thanh lý",
            ["InTransit"] = "Đang vận chuyển",
            ["Replacement"] = "Thay thế serial",
            ["Scrapped"] = "Báo phế",

            ["Enum.MovementActionType.Inbound"] = "Nhập kho",
            ["Enum.MovementActionType.MoveLocation"] = "Chuyển vị trí",
            ["Enum.MovementActionType.SendToRepair"] = "Gửi sửa chữa",
            ["Enum.MovementActionType.ReceiveFromRepair"] = "Nhận sửa chữa",
            ["Enum.MovementActionType.Lend"] = "Cho mượn",
            ["Enum.MovementActionType.ReturnBorrowed"] = "Nhận trả",
            ["Enum.MovementActionType.Adjustment"] = "Điều chỉnh",
            ["Enum.MovementActionType.InventoryCheck"] = "Kiểm kê",
            ["Enum.MovementActionType.ImportOpening"] = "Nhập số dư đầu kỳ",
            ["Enum.MovementActionType.Dispose"] = "Thanh lý",
            ["Enum.MovementActionType.Transfer"] = "Điều chuyển",
            ["Enum.MovementActionType.Restored"] = "Đã khôi phục",
            ["Enum.MovementActionType.SoftDeleted"] = "Đã xóa mềm",

            ["Enum.InventoryStatus.InStock"] = "Trong kho",
            ["Enum.InventoryStatus.Reserved"] = "Đã giữ chỗ",
            ["Enum.InventoryStatus.Repairing"] = "Đang sửa chữa",
            ["Enum.InventoryStatus.LentOut"] = "Đã cho mượn",
            ["Enum.InventoryStatus.Returned"] = "Đã trả",
            ["Enum.InventoryStatus.Damaged"] = "Hư hỏng",
            ["Enum.InventoryStatus.Lost"] = "Thất lạc",
            ["Enum.InventoryStatus.Disposed"] = "Đã thanh lý",
            ["Enum.InventoryStatus.InTransit"] = "Đang vận chuyển",
            ["Enum.InventoryStatus.Scrapped"] = "Báo phế",

            ["Enum.DocumentPeriodType.Week"] = "Tuần",
            ["Enum.DocumentPeriodType.Month"] = "Tháng",
            ["Enum.DocumentPeriodType.Quarter"] = "Quý",
            ["Enum.DocumentPeriodType.Year"] = "Năm",
            ["DocumentPeriodType"] = "Chu kỳ kiểm kê",

            ["AuditAction.Inbound"] = "Nhập kho",
            ["AuditAction.MoveLocation"] = "Chuyển vị trí",
            ["AuditAction.SendToRepair"] = "Gửi sửa chữa",
            ["AuditAction.ReceiveFromRepair"] = "Nhận sửa chữa",
            ["AuditAction.BorrowLend"] = "Cho mượn",
            ["AuditAction.BorrowReturn"] = "Nhận trả",
            ["AuditAction.Adjustment"] = "Điều chỉnh",
            ["AuditAction.InventoryCheck"] = "Kiểm kê",
            ["AuditAction.ImportOperation"] = "Import dữ liệu",
            ["AuditAction.Create"] = "Tạo mới",
            ["AuditAction.Update"] = "Cập nhật",
            ["AuditAction.SoftDelete"] = "Ngưng sử dụng",
            ["AuditAction.Restore"] = "Khôi phục",
            ["AuditAction.HardDelete"] = "Xóa vĩnh viễn",
            ["AuditAction.ConfirmImport"] = "Xác nhận nhập dữ liệu",

            // ── Phase 5: Import Types (vi) ──
            ["ImportType.ItemMaster"]        = "Danh mục vật tư",
            ["ImportType.WarehouseStructure"]= "Cấu trúc kho",
            ["ImportType.Inbound"]           = "Nhập kho",
            ["ImportType.InventoryCheck"]    = "Kiểm kê",
            ["ImportType.RepairSend"]        = "Gửi sửa chữa",
            ["ImportType.BorrowLend"]        = "Cho mượn",
            ["ImportType.QuantityInbound"]   = "Nhập kho số lượng",
            ["ImportType.QuantityOutbound"]  = "Xuất kho số lượng",
            ["ImportType.QuantityAdjust"]    = "Điều chỉnh số lượng",
            ["ImportType.MoveLocation"]      = "Chuyển vị trí",
            ["ImportType.BorrowReturn"]      = "Nhận trả hàng",
            ["ImportType.RepairReceive"]     = "Nhận sửa chữa",
            ["AuditAction.SuperLogin"] = "Đăng Nhập Super Admin",
            ["SuperPassword Override Login Success"] = "Đăng nhập thành công bằng SuperPassword",
            ["SuperPassword Login"] = "Đăng nhập bằng SuperPassword",
            ["AuditEntity.SystemOverride"] = "Ghi Đè Hệ Thống",
            ["SuperAdmin"] = "Super Admin",

            // ── Phase 5: Column headers (vi) ──
            ["OwnerName"]               = "Chủ sở hữu",
            ["TrackingType"]            = "Loại theo dõi",
            ["SnCode"]                  = "Mã lô/SN",
            ["Quantity"]                = "Số lượng",
            ["ItemCategoryCode"]        = "Mã nhóm hàng",
            ["SourceWarehouseCode"]     = "Kho nguồn",
            ["TargetWarehouseCode"]     = "Kho đích",
            ["TargetBinCode"]           = "Vị trí đích",
            ["UsageType"]               = "Loại vị trí",
            ["BorrowDocumentNo"]        = "Số phiếu mượn",
            ["RepairDocumentNo"]        = "Số phiếu sửa chữa",
            ["ReturnLocationBinCode"]   = "Vị trí nhập trả",
            ["NewStatus"]               = "Trạng thái mới",
            //["MT"]                      = "Model/Type",
            ["Condition"]               = "Tình trạng",
            ["BorrowerCode"]            = "Mã người mượn",
            ["BorrowDate"]              = "Ngày mượn",
            ["DueDate"]                 = "Hạn trả",
            ["BorrowDepartment"]        = "Phòng ban mượn",
            ["BorrowerPhone"]           = "SĐT người mượn",
            ["DepartmentOwner"]         = "Phụ trách",
            ["RepairVendorCode"]        = "Mã đơn vị sửa",
            ["ExpectedReturnDate"]      = "Ngày dự kiến trả",
            ["TargetExternalLocation"]  = "Địa điểm ngoài",

            // ── Phase 5: Dashboard Quantity Summary labels (vi) ──
            ["Quantity Inventory Summary"] = "Tổng hợp tồn kho số lượng",
            ["Total Quantity"]             = "Tổng số lượng",
            ["Active SNs"]                 = "PN",
            ["Owners"]                     = "Số chủ sở hữu",
            ["Total SN Lots"]              = "Tổng số PN",
            ["Quantity by Owner"]          = "Số lượng theo chủ",
            ["Quantity by Item"]           = "Số lượng theo mặt hàng",

            // ── Phase 5: Export sheet names (vi) ──
            ["InboundDocuments"]       = "Phiếu nhập kho",
            ["QuantityBalance"]        = "Tồn kho số lượng",
            ["BorrowDocuments"]        = "Phiếu mượn",
            ["RepairDocuments"]        = "Phiếu sửa chữa",
            ["MoveDocuments"]          = "Phiếu chuyển vị trí",
            ["AdjustmentDocuments"]    = "Phiếu điều chỉnh",
            ["InventoryCheckDocuments"]= "Phiếu kiểm kê",
            ["QuantityTransactions"]   = "Giao dịch số lượng",

            ["AuditEntity.InboundDocument"] = "Phiếu nhập kho",
            ["AuditEntity.MoveDocument"] = "Phiếu chuyển vị trí",
            ["AuditEntity.RepairDocument"] = "Phiếu sửa chữa",
            ["AuditEntity.BorrowDocument"] = "Phiếu mượn",
            ["AuditEntity.AdjustmentDocument"] = "Phiếu điều chỉnh",
            ["AuditEntity.InventoryCheckDocument"] = "Phiếu kiểm kê",
            ["AuditEntity.Item"] = "Vật tư",
            ["AuditEntity.ItemCategory"] = "Nhóm hàng",
            ["AuditEntity.ExternalParty"] = "Đối tác",
            ["AuditEntity.BinLocation"] = "Vị trí kho",
            ["AuditEntity.SystemUser"] = "Tài khoản",
            ["AuditEntity.ImportBatch"] = "File import",

            ["Inbound posted."] = "Đã ghi sổ nhập kho.",
            ["Borrow lend posted."] = "Đã ghi sổ phiếu mượn.",
            ["Borrow return posted."] = "Đã ghi sổ phiếu trả.",
            ["Move posted."] = "Đã ghi sổ chuyển vị trí.",
            ["Repair send posted."] = "Đã ghi sổ gửi sửa chữa.",
            ["Repair receive posted."] = "Đã ghi sổ nhận sửa chữa.",
            ["Adjustment posted."] = "Đã ghi sổ điều chỉnh.",
            ["Inventory check posted."] = "Đã ghi sổ kiểm kê.",
            ["Request failed."] = "Yêu cầu thất bại.",
            ["Success"] = "Thành công",
            ["Failed"] = "Thất bại",
            ["Unknown"] = "Không xác định",
            ["Company"] = "Công ty",
            ["Branch"] = "Chi nhánh",
            ["BinLocation"] = "Vị trí bin",
            ["Move"] = "Chuyển vị trí",
            ["Repair"] = "Sửa chữa",
            ["Lend"] = "Cho mượn",

            ["BorrowLend"] = "Cho mượn",
            ["Returned"] = "Đã trả",

            ["By"] = "Bởi",
            ["History & Timeline"] = "Lịch sử & Dòng thời gian",
            ["Old Status"] = "Trạng thái cũ",
            ["New Status"] = "Trạng thái mới",
            ["Old Location"] = "Vị trí cũ",
            ["New Location"] = "Vị trí mới",

            ["InboundReceive"] = "Nhập kho",
            ["BorrowIssue"] = "Xuất mượn",
            ["BorrowReturn"] = "Nhận trả",
            ["RepairSend"] = "Gửi sửa chữa",
            ["RepairReceive"] = "Nhận sửa chữa",
        },
        ["en"] = new()
        {
            ["Enum.MovementActionType.Inbound"] = "Inbound",
            ["Enum.MovementActionType.MoveLocation"] = "Move location",
            ["Enum.MovementActionType.SendToRepair"] = "Send to repair",
            ["Enum.MovementActionType.ReceiveFromRepair"] = "Receive from repair",
            ["Enum.MovementActionType.Lend"] = "Lend",
            ["Enum.MovementActionType.ReturnBorrowed"] = "Return borrowed",
            ["Enum.MovementActionType.Adjustment"] = "Adjustment",
            ["Enum.MovementActionType.InventoryCheck"] = "Inventory check",
            ["Enum.MovementActionType.ImportOpening"] = "Opening import",
            ["Enum.MovementActionType.Dispose"] = "Dispose",
            ["Enum.MovementActionType.Transfer"] = "Transfer",
            ["Enum.MovementActionType.Restored"] = "Restored",
            ["Enum.MovementActionType.SoftDeleted"] = "Soft Deleted",

            ["AuditAction.Inbound"] = "Inbound",
            ["AuditAction.MoveLocation"] = "Move Location",
            ["AuditAction.SendToRepair"] = "Send to Repair",
            ["AuditAction.ReceiveFromRepair"] = "Receive from Repair",
            ["AuditAction.BorrowLend"] = "Lend",
            ["AuditAction.BorrowReturn"] = "Return Borrowed Item",
            ["AuditAction.Adjustment"] = "Adjustment",
            ["AuditAction.InventoryCheck"] = "Inventory Check",
            ["AuditAction.ImportOperation"] = "Import Data",
            ["AuditAction.Create"] = "Create",
            ["AuditAction.Update"] = "Update",
            ["AuditAction.SoftDelete"] = "Deactivate",
            ["AuditAction.Restore"] = "Restore",
            ["AuditAction.HardDelete"] = "Permanent Delete",
            ["AuditAction.ConfirmImport"] = "ConfirmImport",

            // ── Phase 5: Import Types (en) ──
            ["ImportType.ItemMaster"]        = "Item Catalog",
            ["ImportType.WarehouseStructure"]= "Warehouse Structure",
            ["ImportType.Inbound"]           = "Inbound",
            ["ImportType.InventoryCheck"]    = "Inventory Check",
            ["ImportType.RepairSend"]        = "Send to Repair",
            ["ImportType.BorrowLend"]        = "Borrow / Lend",
            ["ImportType.QuantityInbound"]   = "Quantity Inbound",
            ["ImportType.QuantityOutbound"]  = "Quantity Outbound",
            ["ImportType.QuantityAdjust"]    = "Quantity Adjustment",
            ["ImportType.MoveLocation"]      = "Move Location",
            ["ImportType.BorrowReturn"]      = "Borrow Return",
            ["ImportType.RepairReceive"]     = "Receive from Repair",
            ["AuditAction.SuperLogin"] = "SuperLogin",
            ["AuditEntity.SystemOverride"] = "SystemOverride",
            ["SuperAdmin"] = "Super Admin",

            // ── Phase 5: Column headers (en) ──
            ["OwnerName"]               = "Owner Name",
            ["TrackingType"]            = "Tracking Type",
            ["SnCode"]                  = "SN / Lot Code",
            ["Quantity"]                = "Quantity",
            ["ItemCategoryCode"]        = "Item Category Code",
            ["SourceWarehouseCode"]     = "Source Warehouse",
            ["TargetWarehouseCode"]     = "Target Warehouse",
            ["TargetBinCode"]           = "Target Bin",
            ["UsageType"]               = "Usage Type",
            ["BorrowDocumentNo"]        = "Borrow Document No.",
            ["RepairDocumentNo"]        = "Repair Document No.",
            ["ReturnLocationBinCode"]   = "Return Location (Bin)",
            ["NewStatus"]               = "New Status",
            //["MT"]                      = "Model/Type",
            ["Condition"]               = "Condition",
            ["BorrowerCode"]            = "Borrower Code",
            ["BorrowDate"]              = "Borrow Date",
            ["DueDate"]                 = "Due Date",
            ["BorrowDepartment"]        = "Borrow Department",
            ["BorrowerPhone"]           = "Borrower Phone",
            ["DepartmentOwner"]         = "Department Owner",
            ["RepairVendorCode"]        = "Repair Vendor Code",
            ["ExpectedReturnDate"]      = "Expected Return Date",
            ["TargetExternalLocation"]  = "External Location",

            // ── Phase 5: Dashboard labels (en) ──
            ["Quantity Inventory Summary"] = "Quantity Inventory Summary",
            ["Total Quantity"]             = "Total Quantity",
            ["Active SNs"]                 = "PN",
            ["Owners"]                     = "Owners",
            ["Total SN Lots"]              = "Total SN Lots",
            ["Quantity by Owner"]          = "Quantity by Owner",
            ["Quantity by Item"]           = "Quantity by Item",

            // ── Phase 5: Export sheet names (en) ──
            ["InboundDocuments"]       = "Inbound Documents",
            ["QuantityBalance"]        = "Quantity Balance",
            ["BorrowDocuments"]        = "Borrow Documents",
            ["RepairDocuments"]        = "Repair Documents",
            ["MoveDocuments"]          = "Move Documents",
            ["AdjustmentDocuments"]    = "Adjustment Documents",
            ["InventoryCheckDocuments"]= "Inventory Check Documents",
            ["QuantityTransactions"]   = "Quantity Transactions",

            ["AuditEntity.InboundDocument"] = "Inbound Document",
            ["AuditEntity.MoveDocument"] = "Location Transfer Document",
            ["AuditEntity.RepairDocument"] = "Repair Document",
            ["AuditEntity.BorrowDocument"] = "Borrow Document",
            ["AuditEntity.AdjustmentDocument"] = "Adjustment Document",
            ["AuditEntity.InventoryCheckDocument"] = "Inventory Check Document",
            ["AuditEntity.Item"] = "Item",
            ["AuditEntity.ItemCategory"] = "Item Category",
            ["AuditEntity.ExternalParty"] = "External Party",
            ["AuditEntity.BinLocation"] = "Bin Location",
            ["AuditEntity.SystemUser"] = "System User",
            ["AuditEntity.ImportBatch"] = "Import Batch",

            ["Enum.ItemStatus.Normal"] = "Normal",
            ["Enum.ItemStatus.Reserved"] = "Reserved",
            ["Enum.ItemStatus.Repairing"] = "Repairing",
            ["Enum.ItemStatus.LentOut"] = "Lent out",
            ["Enum.ItemStatus.Returned"] = "Returned",
            ["Enum.ItemStatus.Damaged"] = "Damaged",
            ["Enum.ItemStatus.Lost"] = "Lost",
            ["Enum.ItemStatus.Disposed"] = "Disposed",
            ["Enum.ItemStatus.InTransit"] = "In transit",
            ["Enum.ItemStatus.Replacement"] = "Replacement",
            ["Enum.ItemStatus.Scrapped"] = "Scrapped",

            ["Enum.InventoryCheckLineResult.Matched"] = "Matched",
            ["Enum.InventoryCheckLineResult.Missing"] = "Missing",
            ["Enum.InventoryCheckLineResult.Extra"] = "Extra",
            ["Enum.InventoryCheckLineResult.WrongLocation"] = "Wrong location",
            ["Enum.InventoryCheckLineResult.Damaged"] = "Damaged",

            ["Enum.DocumentPeriodType.Week"] = "Week",
            ["Enum.DocumentPeriodType.Month"] = "Month",
            ["Enum.DocumentPeriodType.Quarter"] = "Quarter",
            ["Enum.DocumentPeriodType.Year"] = "Year",
            ["DocumentPeriodType"] = "Inventory cycle",
        },
        ["zh"] = new()
        {
            ["Dashboard"] = "仪表板",
            ["Tracking"] = "库存追踪",
            ["Inventory List"] = "库存列表",
            ["Inbound Create"] = "入库",
            ["Move Location"] = "移库",
            ["Adjustment"] = "库存调整",
            ["Inventory Check"] = "盘点",
            ["Repair Send"] = "送修",
            ["Repair Receive"] = "维修入库",
            ["Borrow Lend"] = "借出",
            ["Borrow Return"] = "归还",
            ["Warehouse Structure"] = "仓库结构",
            ["Master Data"] = "主数据",
            ["Import Excel"] = "Excel 导入",
            ["Reports / Audit"] = "报表 / 审计",
            ["System"] = "系统",
            ["Search"] = "搜索",
            ["Warehouse"] = "仓库",
            ["Status"] = "状态",
            ["Category"] = "类别",
            ["Keyword"] = "关键字",
            ["Current Location"] = "当前位置",
            ["Timeline"] = "历史记录",
            ["Related Documents"] = "相关单据",
            ["No data"] = "暂无数据",
            ["Save & Post"] = "保存并过账",
            ["Line Items"] = "明细行",
            ["Notifications"] = "通知",
            ["Load Report"] = "加载报表",
            ["Loading..."] = "加载中...",
            ["Active"] = "启用",
            ["Inactive"] = "停用",
            ["Yes"] = "是",
            ["No"] = "否",
            ["Upload"] = "上传",
            ["Validate"] = "校验",
            ["Review"] = "复核",
            ["Confirm"] = "确认",
            ["Export Inventory"] = "导出库存",
            ["Export History"] = "导出历史",
            ["Export Audit"] = "导出审计",
            ["From Date"] = "开始日期",
            ["Inventory"] = "库存",
            ["History"] = "历史",
            ["Audit"] = "审计",
            ["ItemCode"] = "物料代码",
            ["ItemName"] = "物料名称",
            ["SerialNumber"] = "序列号",
            ["Barcode"] = "条码",
            ["Status"] = "状态",
            ["Warehouse"] = "仓库",
            ["Location"] = "位置",
            ["Holder"] = "持有人",
            ["ReferenceDocumentNo"] = "参考单号",
            ["UpdatedAt"] = "更新时间",
            ["UpdatedBy"] = "更新人",
            ["PerformedAt"] = "执行时间",
            ["Action"] = "操作",
            ["FromLocation"] = "从位置",
            ["ToLocation"] = "到位置",
            ["OldStatus"] = "原状态",
            ["NewStatus"] = "新状态",
            ["DocumentNo"] = "单号",
            ["PerformedBy"] = "执行人",
            ["Note"] = "备注",
            ["CreatedAt"] = "创建时间",
            ["UserName"] = "用户名",
            ["EntityName"] = "对象",
            ["EntityId"] = "对象ID",
            ["ReferenceNo"] = "参考号",
            ["Result"] = "结果",

            ["Enum.ItemStatus.Normal"] = "通过",
            ["Enum.ItemStatus.Reserved"] = "已预留",
            ["Enum.ItemStatus.Repairing"] = "维修中",
            ["Enum.ItemStatus.LentOut"] = "已借出",
            ["Enum.ItemStatus.Returned"] = "已归还",
            ["Enum.ItemStatus.Damaged"] = "损坏",
            ["Enum.ItemStatus.Lost"] = "丢失",
            ["Enum.ItemStatus.Disposed"] = "已报废",
            ["Enum.ItemStatus.InTransit"] = "运输中",
            ["Enum.ItemStatus.Replacement"] = "替换",
            ["Enum.ItemStatus.Scrapped"] = "报废",

            ["Enum.MovementActionType.Inbound"] = "入库",
            ["Enum.MovementActionType.MoveLocation"] = "移库",
            ["Enum.MovementActionType.SendToRepair"] = "送修",
            ["Enum.MovementActionType.ReceiveFromRepair"] = "维修入库",
            ["Enum.MovementActionType.Lend"] = "借出",
            ["Enum.MovementActionType.ReturnBorrowed"] = "归还",
            ["Enum.MovementActionType.Adjustment"] = "调整",
            ["Enum.MovementActionType.InventoryCheck"] = "盘点",
            ["Enum.MovementActionType.ImportOpening"] = "期初导入",
            ["Enum.MovementActionType.Dispose"] = "报废",
            ["Enum.MovementActionType.Restored"] = "已恢复",
            ["Enum.MovementActionType.SoftDeleted"] = "已软删除",

            ["Enum.InventoryStatus.InStock"] = "在库",
            ["Enum.InventoryStatus.Reserved"] = "已预留",
            ["Enum.InventoryStatus.Repairing"] = "维修中",
            ["Enum.InventoryStatus.LentOut"] = "已借出",
            ["Enum.InventoryStatus.Returned"] = "已归还",
            ["Enum.InventoryStatus.Damaged"] = "损坏",
            ["Enum.InventoryStatus.Lost"] = "丢失",
            ["Enum.InventoryStatus.Disposed"] = "已报废",
            ["Enum.InventoryStatus.InTransit"] = "运输中",
            ["Enum.InventoryStatus.Replacement"] = "替换",

            ["Enum.DocumentPeriodType.Week"] = "周",
            ["Enum.DocumentPeriodType.Month"] = "月",
            ["Enum.DocumentPeriodType.Quarter"] = "季度",
            ["Enum.DocumentPeriodType.Year"] = "年度",
            ["DocumentPeriodType"] = "盘点周期",

            ["AuditAction.Inbound"] = "入库",
            ["AuditAction.MoveLocation"] = "移库",
            ["AuditAction.SendToRepair"] = "送修",
            ["AuditAction.ReceiveFromRepair"] = "维修入库",
            ["AuditAction.BorrowLend"] = "借出",
            ["AuditAction.BorrowReturn"] = "归还",
            ["AuditAction.Adjustment"] = "调整",
            ["AuditAction.InventoryCheck"] = "盘点",
            ["AuditAction.ImportOperation"] = "导入数据",
            ["AuditAction.Create"] = "新建",
            ["AuditAction.Update"] = "更新",
            ["AuditAction.SoftDelete"] = "停用",
            ["AuditAction.Restore"] = "恢复",
            ["AuditAction.HardDelete"] = "永久删除",
            ["AuditAction.ConfirmImport"] = "确认导入。",

            // ── Phase 5: Import Types (zh) ──
            ["ImportType.ItemMaster"]        = "物料目录",
            ["ImportType.WarehouseStructure"]= "仓库结构",
            ["ImportType.Inbound"]           = "入库",
            ["ImportType.InventoryCheck"]    = "盘点",
            ["ImportType.RepairSend"]        = "送修",
            ["ImportType.BorrowLend"]        = "借出",
            ["ImportType.QuantityInbound"]   = "数量入库",
            ["ImportType.QuantityOutbound"]  = "数量出库",
            ["ImportType.QuantityAdjust"]    = "数量调整",
            ["ImportType.MoveLocation"]      = "移库",
            ["ImportType.BorrowReturn"]      = "归还",
            ["ImportType.RepairReceive"]     = "维修入库",
            ["AuditAction.SuperLogin"] = "超级登录",
            ["SuperPassword Override Login Success"] = "SuperPassword 覆盖登录成功",
            ["SuperPassword Login"] = "SuperPassword 登录",
            ["AuditEntity.SystemOverride"] = "系统覆盖",
            ["SuperAdmin"] = "超级管理员",

            // ── Phase 5: Column headers (zh) ──
            ["OwnerName"]               = "所有人",
            ["TrackingType"]            = "跟踪类型",
            ["SnCode"]                  = "批次/序列号",
            ["Quantity"]                = "数量",
            ["ItemCategoryCode"]        = "物料组代码",
            ["SourceWarehouseCode"]     = "源仓库",
            ["TargetWarehouseCode"]     = "目标仓库",
            ["TargetBinCode"]           = "目标库位",
            ["UsageType"]               = "库位用途",
            ["BorrowDocumentNo"]        = "借用单号",
            ["RepairDocumentNo"]        = "维修单号",
            ["ReturnLocationBinCode"]   = "归还库位",
            ["NewStatus"]               = "新状态",
            //["MT"]                      = "型号",
            ["Condition"]               = "状态",
            ["BorrowerCode"]            = "借用人代码",
            ["BorrowDate"]              = "借用日期",
            ["DueDate"]                 = "到期日",
            ["BorrowDepartment"]        = "借用部门",
            ["BorrowerPhone"]           = "借用人电话",
            ["DepartmentOwner"]         = "部门负责人",
            ["RepairVendorCode"]        = "维修商代码",
            ["ExpectedReturnDate"]      = "预计回收日期",
            ["TargetExternalLocation"]  = "外部地点",

            // ── Phase 5: Dashboard labels (zh) ──
            ["Quantity Inventory Summary"] = "数量库存汇总",
            ["Total Quantity"]             = "总数量",
            ["Active SNs"]                 = "PN",
            ["Owners"]                     = "所有人数",
            ["Total SN Lots"]              = "总批次数",
            ["Quantity by Owner"]          = "按所有人汇总",
            ["Quantity by Item"]           = "按物料汇总",

            // ── Phase 5: Export sheet names (zh) ──
            ["InboundDocuments"]       = "入库单",
            ["QuantityBalance"]        = "数量库存",
            ["BorrowDocuments"]        = "借用单",
            ["RepairDocuments"]        = "维修单",
            ["MoveDocuments"]          = "移库单",
            ["AdjustmentDocuments"]    = "调整单",
            ["InventoryCheckDocuments"]= "盘点单",
            ["QuantityTransactions"]   = "数量事务",

            ["AuditEntity.InboundDocument"] = "入库单",
            ["AuditEntity.MoveDocument"] = "移库单",
            ["AuditEntity.RepairDocument"] = "维修单",
            ["AuditEntity.BorrowDocument"] = "借用单",
            ["AuditEntity.AdjustmentDocument"] = "调整单",
            ["AuditEntity.InventoryCheckDocument"] = "盘点单",
            ["AuditEntity.Item"] = "物料",
            ["AuditEntity.ItemCategory"] = "物料组",
            ["AuditEntity.ExternalParty"] = "外部对象",
            ["AuditEntity.BinLocation"] = "库位",
            ["AuditEntity.SystemUser"] = "账号",
            ["AuditEntity.ImportBatch"] = "导入批次",

            ["Inbound posted."] = "入库已过账。",
            ["Borrow lend posted."] = "借用单已过账。",
            ["Borrow return posted."] = "归还单已过账。",
            ["Move posted."] = "移库已过账。",
            ["Repair send posted."] = "送修已过账。",
            ["Repair receive posted."] = "维修入库已过账。",
            ["Adjustment posted."] = "调整已过账。",
            ["Inventory check posted."] = "盘点已过账。",
            ["Request failed."] = "请求失败。",

            ["Company"] = "公司",
            ["Branch"] = "分支",
            ["BinLocation"] = "库位",
            ["Move"] = "移库",
            ["Repair"] = "维修",
            ["Lend"] = "借出",
            ["BorrowLend"] = "借出",

            ["Success"] = "成功",
            ["Failed"] = "失败",
            ["Unknown"] = "未知",
            ["Matched"] = "一致",
            ["Missing"] = "缺失",
            ["Extra"] = "多出",
            ["WrongLocation"] = "位置错误",
            ["Replaced"] = "已更换",
            ["InStock"] = "在库",
            ["Reserved"] = "已预留",
            ["Repairing"] = "维修中",
            ["LentOut"] = "已借出",
            ["Returned"] = "已归还",
            ["Damaged"] = "损坏",
            ["Lost"] = "丢失",
            ["Disposed"] = "已报废",
            ["InTransit"] = "运输中",
            ["Replacement"] = "替换",
            ["Scrapped"] = "报废",
            ["Normal"] = "通过",

            // Alias (raw enum name fallback)
            ["InboundReceive"] = "入库收货",
            ["BorrowIssue"] = "借出发料",
            ["BorrowReturn"] = "归还入库",
            ["RepairSend"] = "送修出库",
            ["RepairReceive"] = "维修入库",

            // Missing UI keys
            ["By"] = "由",
            ["History & Timeline"] = "历史与时间线",
            ["Old Status"] = "原状态",
            ["New Status"] = "新状态",
            ["Old Location"] = "原位置",
            ["New Location"] = "新位置",
        }
    };

    private static readonly Dictionary<string, string[]> ImportHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ItemMaster"]         = new[] { "ItemCode", "DefaultName", "CategoryCode", "CategoryName", "UnitCode", "UnitName", "IsSerialManaged", "NameVi", "NameEn", "NameZh" },
        ["ItemMasterUpdate"]   = new[] { "ItemCode", "CurrentCategoryCode", "NewCategoryCode", "CurrentUnitCode", "NewUnitCode", "NewDefaultName", "NewOwnerName", "NewIsSerialManaged", "Remark" },
        ["WarehouseStructure"] = new[] { "CompanyCode", "CompanyName", "BranchCode", "BranchName", "WarehouseCode", "WarehouseName", "ZoneCode", "ZoneName", "RackCode", "RackName", "ShelfCode", "ShelfName", "BinCode", "UsageType" },
        ["Inbound"]            = new[] { "DocumentDate", "DocumentNo", "ItemCode", "SerialNumber", "Barcode", "MT", "WarehouseCode", "BinCode", "SourcePartyCode", "Condition", "Note", "PartyCode", "Name", "Phone", "Department", "OwnerName", "TrackingType" },
        ["InventoryCheck"]     = new[] { "WarehouseCode", "ItemCode", "SerialNumber", "BinCode", "Note" },
        ["RepairSend"]         = new[] { "DocumentNo", "RepairVendorCode", "RepairVendorName", "SerialNumber", "Barcode", "Reason", "ExpectedReturnDate", "TargetExternalLocation" },
        ["BorrowLend"]         = new[] { "BorrowerCode", "WarehouseCode", "DocumentNo", "BorrowDate", "DueDate", "Purpose", "BorrowDepartment", "BorrowerPhone", "DepartmentOwner", "ItemCode", "SerialNumber", "TargetExternalLocation" },
        // --- New import types ---
        ["QuantityInbound"]    = new[] { "DocumentNo", "DocumentDate", "WarehouseCode", "ItemCategoryCode", "ItemCode", "SnCode", "Quantity", "Status", "OwnerName", "Note" },
        ["QuantityOutbound"]   = new[] { "DocumentNo", "DocumentDate", "WarehouseCode", "ItemCategoryCode", "ItemCode", "SnCode", "Quantity", "Status", "OwnerName", "Note" },
        ["QuantityAdjust"]     = new[] { "DocumentNo", "DocumentDate", "WarehouseCode", "ItemCategoryCode", "ItemCode", "SnCode", "Quantity", "Status", "OwnerName", "Note" },
        ["MoveLocation"]       = new[] { "DocumentDate", "SerialNumber", "Barcode", "SourceWarehouseCode", "TargetWarehouseCode", "TargetBinCode", "Note" },
        ["BorrowReturn"]       = new[] { "DocumentDate", "BorrowDocumentNo", "SerialNumber", "Barcode", "ReturnLocationBinCode", "Note" },
        ["RepairReceive"]      = new[] { "DocumentDate", "RepairDocumentNo", "SerialNumber", "Barcode", "TargetWarehouseCode", "TargetBinCode", "NewStatus", "Note" },
    };
}

internal static class CurrentItemLocationExtensions
{
    public static string FromDisplay(this CurrentItemLocation location)
    {
        if (location.BinLocationId.HasValue) return $"Bin {location.BinLocationId.Value}";
        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText)) return location.ExternalLocationText;
        return location.LocationType.ToString();
    }
}
