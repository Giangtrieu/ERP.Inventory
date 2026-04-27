using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class ImportExportService : IImportService, IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly InventoryDbContext _db;
    private readonly IDocumentNumberService _documentNumbers;

    public ImportExportService(InventoryDbContext db, IDocumentNumberService documentNumbers)
    {
        _db = db;
        _documentNumbers = documentNumbers;
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

        var rows = (await SimpleExcel.ReadTableAsync(fileStream, fileName, cancellationToken))
            .Where(x => !IsInstructionRow(x))
            .ToArray();
        if (rows.Length == 0)
        {
            return ServiceResult<int>.Fail("File does not contain data rows.");
        }

        var batch = new ImportBatch
        {
            BatchNo = _documentNumbers.Next("IMP", DateTime.Now),
            ImportType = importType,
            FileName = fileName,
            Status = ImportBatchStatus.Uploaded,
            TotalRows = rows.Length,
            CreatedBy = user.UserName
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        var rowNumber = 2;
        foreach (var row in rows)
        {
            _db.ImportBatchRows.Add(new ImportBatchRow
            {
                ImportBatchId = batch.Id,
                RowNumber = rowNumber++,
                RawJson = JsonSerializer.Serialize(row, JsonOptions),
                Severity = ValidationSeverity.Info,
                IsValid = true,
                CreatedBy = user.UserName
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
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

        var blocking = 0;
        var dataRows = batch.Rows.OrderBy(x => x.RowNumber).Select(x => new { Row = x, Data = Row(x) }).ToArray();
        var batchErrors = BuildBatchValidationErrors(batch.ImportType, dataRows.Select(x => (x.Row, x.Data)).ToArray());
        foreach (var rowData in dataRows)
        {
            var row = rowData.Row;
            var errors = await ValidateRowAsync(batch.ImportType, rowData.Data, user, cancellationToken);
            if (batchErrors.TryGetValue(row.Id, out var extraErrors))
            {
                errors.AddRange(extraErrors);
            }

            row.IsValid = errors.Count == 0;
            row.Severity = errors.Count == 0 ? ValidationSeverity.Info : ValidationSeverity.Blocking;
            row.Message = errors.Count == 0 ? "OK" : string.Join("; ", errors);
            row.SuggestedFix = errors.Count == 0 ? null : "Correct the row and upload again.";
            row.UpdatedAt = DateTime.Now;
            row.UpdatedBy = user.UserName;
            if (errors.Count > 0)
            {
                blocking++;
            }
        }

        batch.BlockingErrorRows = blocking;
        batch.Status = blocking == 0 ? ImportBatchStatus.Validated : ImportBatchStatus.Blocked;
        batch.UpdatedAt = DateTime.Now;
        batch.UpdatedBy = user.UserName;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<int>.Ok(blocking, blocking == 0 ? "Import batch is valid." : "Import batch has blocking errors.");
    }

    public async Task<ServiceResult<int>> ConfirmAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var validate = await ValidateAsync(importBatchId, user, cancellationToken);
        if (!validate.Success)
        {
            return validate;
        }

        var batch = await _db.ImportBatches.Include(x => x.Rows).FirstAsync(x => x.Id == importBatchId, cancellationToken);
        if (batch.BlockingErrorRows > 0)
        {
            return ServiceResult<int>.Fail("Import batch has blocking errors.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var rows = batch.Rows.OrderBy(x => x.RowNumber).Select(Row).ToArray();
        var inserted = batch.ImportType switch
        {
            "ItemMaster" => await ConfirmItemMasterAsync(rows, user, cancellationToken),
            "WarehouseStructure" => await ConfirmWarehouseStructureAsync(rows, user, cancellationToken),
            "Inbound" => await ConfirmInboundAsync(rows, user, cancellationToken),
            "InventoryCheck" => await ConfirmInventoryCheckAsync(rows, user, cancellationToken),
            "RepairSend" => await ConfirmRepairSendAsync(rows, user, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported import type.")
        };

        batch.Status = ImportBatchStatus.Confirmed;
        batch.UpdatedAt = DateTime.Now;
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
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return ServiceResult<int>.Ok(inserted, "Import confirmed.");
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
                BlockingErrorRows = x.BlockingErrorRows
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

        var bytes = SimpleExcel.CreateWorkbook(headers, TemplateRows(importType), $"{importType}Template");
        return Task.FromResult(bytes);
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
            .AsQueryable();

        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Item != null && x.ItemInstance.Item.CategoryId == filter.CategoryId.Value);
        }

        if (!user.IsAdmin)
        {
            query = query.Where(x => x.WarehouseId == null || user.WarehouseIds.Contains(x.WarehouseId.Value));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.ItemInstance != null && x.ItemInstance.Status == status);
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
                 x.ItemInstance.Item.DefaultName.Contains(key) ||
                 (x.ItemInstance.SerialNumber != null && x.ItemInstance.SerialNumber.Contains(key)) ||
                 (x.ItemInstance.Barcode != null && x.ItemInstance.Barcode.Contains(key))));
        }

        var rows = await query.OrderBy(x => x.ItemInstance!.Item!.ItemCode)
            .Take(10000)
            .Select(x => new object?[]
            {
                x.ItemInstance!.Item!.ItemCode,
                x.ItemInstance.Item.DefaultName,
                x.ItemInstance.SerialNumber,
                x.ItemInstance.Barcode,
                ExcelText(user, $"Enum.ItemStatus.{x.ItemInstance.Status}"),
                x.Warehouse != null ? x.Warehouse.WarehouseCode : string.Empty,
                x.BinLocation != null ? x.BinLocation.FullPath : string.Empty,
                x.ExternalParty != null ? x.ExternalParty.Name : string.Empty,
                x.ReferenceDocumentNo,
                x.UpdatedLocationAt,
                x.UpdatedLocationBy
            })
            .ToArrayAsync(cancellationToken);

        return SimpleExcel.CreateWorkbook(Headers(user, "ItemCode", "ItemName", "SerialNumber", "Barcode", "Status", "Warehouse", "Location", "Holder", "ReferenceDocumentNo", "UpdatedAt", "UpdatedBy"), rows, ExcelText(user, "Inventory"));
    }

    public async Task<byte[]> ExportHistoryAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var query = _db.ItemMovementHistories.AsNoTracking().Include(x => x.ItemInstance)!.ThenInclude(x => x!.Item).AsQueryable();

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
            query = query.Where(x => _db.CurrentItemLocations.Any(c => c.ItemInstanceId == x.ItemInstanceId && c.WarehouseId == filter.WarehouseId.Value));
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
            query = query.Where(x => _db.CurrentItemLocations.Any(c =>
                c.ItemInstanceId == x.ItemInstanceId &&
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
            .Select(x => new object?[] { x.CreatedAt, x.UserName, x.Action, x.EntityName, x.EntityId, x.ReferenceNo, x.Result })
            .ToArrayAsync(cancellationToken);

        return SimpleExcel.CreateWorkbook(Headers(user, "CreatedAt", "UserName", "Action", "EntityName", "EntityId", "ReferenceNo", "Result"), rows, ExcelText(user, "Audit"));
    }

    private async Task<List<string>> ValidateRowAsync(string importType, Dictionary<string, string> row, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var errors = RequiredColumns(importType).Where(x => string.IsNullOrWhiteSpace(Value(row, x))).Select(x => $"{x} is required.").ToList();
        if (errors.Count > 0)
        {
            return errors;
        }

        switch (importType)
        {
            case "Inbound":
                await ValidateInboundRowAsync(row, errors, user, cancellationToken);
                break;
            case "InventoryCheck":
                await ValidateInventoryCheckRowAsync(row, errors, user, cancellationToken);
                break;
            case "RepairSend":
                await ValidateRepairSendRowAsync(row, errors, user, cancellationToken);
                break;
        }

        return errors;
    }

    private async Task ValidateInboundRowAsync(Dictionary<string, string> row, List<string> errors, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(Value(row, "ItemCode"), cancellationToken);
        if (item == null)
        {
            errors.Add("ItemCode does not exist.");
        }
        else if (item.IsSerialManaged && string.IsNullOrWhiteSpace(Value(row, "SerialNumber")))
        {
            errors.Add("SerialNumber is required for serial-managed item.");
        }

        var warehouse = await FindWarehouseAsync(Value(row, "WarehouseCode"), cancellationToken);
        if (warehouse == null)
        {
            errors.Add("WarehouseCode does not exist.");
        }
        else if (!user.CanAccessWarehouse(warehouse.Id))
        {
            errors.Add("Current user cannot import into this warehouse.");
        }

        if (warehouse != null && await FindBinAsync(warehouse.Id, Value(row, "BinCode"), cancellationToken) == null)
        {
            errors.Add("BinCode is invalid for warehouse.");
        }
        else if (warehouse != null)
        {
            var bin = await FindBinAsync(warehouse.Id, Value(row, "BinCode"), cancellationToken);
            if (bin != null && await _db.CurrentItemLocations.AnyAsync(x =>
                x.BinLocationId == bin.Id &&
                x.ItemInstance != null &&
                x.ItemInstance.IsActive &&
                x.ItemInstance.Status != ItemStatus.Lost &&
                x.ItemInstance.Status != ItemStatus.Disposed, cancellationToken))
            {
                errors.Add("BinCode already contains another active item.");
            }
        }

        var serial = Value(row, "SerialNumber");
        if (!string.IsNullOrWhiteSpace(serial) && await _db.ItemInstances.AnyAsync(x => x.SerialNumber == serial, cancellationToken))
        {
            errors.Add("SerialNumber already exists.");
        }
    }

    private async Task ValidateInventoryCheckRowAsync(Dictionary<string, string> row, List<string> errors, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var warehouse = await FindWarehouseAsync(Value(row, "WarehouseCode"), cancellationToken);
        if (warehouse == null)
        {
            errors.Add("WarehouseCode does not exist.");
            return;
        }

        if (!user.CanAccessWarehouse(warehouse.Id))
        {
            errors.Add("Current user cannot check this warehouse.");
        }

        if (!Enum.TryParse<InventoryCheckLineResult>(Value(row, "Result"), true, out _))
        {
            errors.Add("Result is invalid.");
        }
    }

    private async Task ValidateRepairSendRowAsync(Dictionary<string, string> row, List<string> errors, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var vendor = await _db.ExternalParties.AsNoTracking().FirstOrDefaultAsync(x => x.PartyCode == Value(row, "RepairVendorCode") && x.PartyType == ExternalPartyType.RepairVendor && x.IsActive, cancellationToken);
        if (vendor == null)
        {
            errors.Add("RepairVendorCode does not exist.");
        }

        if (string.IsNullOrWhiteSpace(Value(row, "TargetExternalLocation")))
        {
            errors.Add("TargetExternalLocation is required.");
        }

        var instance = await FindInstanceAsync(row, cancellationToken);
        if (instance == null)
        {
            errors.Add("SerialNumber or Barcode does not exist.");
        }
        else if (instance.Status != ItemStatus.InStock && instance.Status != ItemStatus.Damaged)
        {
            errors.Add("Only InStock or Damaged item can be sent to repair.");
        }
        else
        {
            var current = await _db.CurrentItemLocations.AsNoTracking().FirstOrDefaultAsync(x => x.ItemInstanceId == instance.Id, cancellationToken);
            if (current?.WarehouseId != null && !user.CanAccessWarehouse(current.WarehouseId.Value))
            {
                errors.Add("Current user cannot send this item to repair.");
            }
        }
    }

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
            if (!await _db.BinLocations.AnyAsync(x => x.WarehouseId == warehouse.Id && x.BinCode == binCode, cancellationToken))
            {
                _db.BinLocations.Add(new BinLocation
                {
                    WarehouseId = warehouse.Id,
                    ShelfId = shelf.Id,
                    BinCode = binCode,
                    FullPath = $"{warehouse.WarehouseCode} / {zone.ZoneCode} / {rack.RackCode} / {shelf.ShelfCode} / {binCode}",
                    CreatedBy = user.UserName
                });
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ConfirmInboundAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        var importSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var serial = NullIfEmpty(Value(row, "SerialNumber"));
            if (serial != null && !importSerials.Add(serial))
            {
                throw new InvalidOperationException($"SerialNumber {serial} is duplicated in this import file.");
            }

            var barcode = NullIfEmpty(Value(row, "Barcode"));
            if (barcode != null && !importBarcodes.Add(barcode))
            {
                throw new InvalidOperationException($"Barcode {barcode} is duplicated in this import file.");
            }
        }

        foreach (var group in rows.GroupBy(x => new { WarehouseCode = Value(x, "WarehouseCode"), SourcePartyCode = Value(x, "SourcePartyCode") }))
        {
            var warehouse = await FindWarehouseAsync(group.Key.WarehouseCode, cancellationToken) ?? throw new InvalidOperationException("Warehouse not found.");
            var sourceParty = string.IsNullOrWhiteSpace(group.Key.SourcePartyCode) ? null : await _db.ExternalParties.FirstOrDefaultAsync(x => x.PartyCode == group.Key.SourcePartyCode, cancellationToken);
            var document = new InboundDocument
            {
                DocumentNo = _documentNumbers.Next("INB", DateTime.Now),
                DocumentDate = DateTime.Now,
                WarehouseId = warehouse.Id,
                SourceExternalPartyId = sourceParty?.Id,
                CreatedBy = user.UserName,
                ApprovedBy = user.UserName,
                ApprovedAt = DateTime.Now,
                PostedAt = DateTime.Now
            };
            _db.InboundDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var row in group)
            {
                var item = await FindItemAsync(Value(row, "ItemCode"), cancellationToken) ?? throw new InvalidOperationException("Item not found.");
                var bin = await FindBinAsync(warehouse.Id, Value(row, "BinCode"), cancellationToken) ?? throw new InvalidOperationException("Bin not found.");
                var serial = NullIfEmpty(Value(row, "SerialNumber"));
                var barcode = NullIfEmpty(Value(row, "Barcode"));
                if (serial != null && await _db.ItemInstances.AnyAsync(x => x.SerialNumber == serial, cancellationToken))
                {
                    throw new InvalidOperationException($"SerialNumber {serial} already exists.");
                }

                if (barcode != null && await _db.ItemInstances.AnyAsync(x => x.Barcode == barcode, cancellationToken))
                {
                    throw new InvalidOperationException($"Barcode {barcode} already exists.");
                }

                if (await _db.CurrentItemLocations.AnyAsync(x =>
                    x.BinLocationId == bin.Id &&
                    x.ItemInstance != null &&
                    x.ItemInstance.IsActive &&
                    x.ItemInstance.Status != ItemStatus.Lost &&
                    x.ItemInstance.Status != ItemStatus.Disposed, cancellationToken))
                {
                    throw new InvalidOperationException($"BinCode {bin.BinCode} already contains another active item.");
                }

                var instance = new ItemInstance
                {
                    ItemId = item.Id,
                    SerialNumber = serial,
                    Barcode = barcode,
                    Status = ItemStatus.InStock,
                    CreatedBy = user.UserName
                };
                _db.ItemInstances.Add(instance);
                await _db.SaveChangesAsync(cancellationToken);
                _db.InboundDocumentLines.Add(new InboundDocumentLine { InboundDocumentId = document.Id, ItemId = item.Id, ItemInstanceId = instance.Id, SerialNumber = instance.SerialNumber, Barcode = instance.Barcode, Quantity = 1, BinLocationId = bin.Id, Condition = Value(row, "Condition"), Note = Value(row, "Note"), CreatedBy = user.UserName });
                _db.CurrentItemLocations.Add(new CurrentItemLocation { ItemInstanceId = instance.Id, LocationType = LocationType.BinLocation, WarehouseId = warehouse.Id, BinLocationId = bin.Id, ReferenceDocumentType = nameof(InboundDocument), ReferenceDocumentId = document.Id, ReferenceDocumentNo = document.DocumentNo, UpdatedLocationAt = DateTime.Now, UpdatedLocationBy = user.UserName, CreatedBy = user.UserName });
                await ApplyStockDeltaAsync(warehouse.Id, bin.Id, item.Id, ItemStatus.InStock, 1, user, cancellationToken);
                AddHistory(instance.Id, MovementActionType.Inbound, "Excel import", bin.FullPath, ItemStatus.Reserved, ItemStatus.InStock, nameof(InboundDocument), document.Id, document.DocumentNo, user);
                AddTransaction(InventoryTransactionType.Inbound, item.Id, instance.Id, warehouse.Id, bin.Id, 1, ItemStatus.InStock, nameof(InboundDocument), document.Id, document.DocumentNo, user);
                count++;
            }
        }

        return count;
    }

    private async Task<int> ConfirmInventoryCheckAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => Value(x, "WarehouseCode")))
        {
            var warehouse = await FindWarehouseAsync(group.Key, cancellationToken) ?? throw new InvalidOperationException("Warehouse not found.");
            var document = new InventoryCheckDocument { DocumentNo = _documentNumbers.Next("CHK", DateTime.Now), DocumentDate = DateTime.Now, WarehouseId = warehouse.Id, CountMethod = "Excel", ResponsibleStaff = user.UserName, CreatedBy = user.UserName, ApprovedBy = user.UserName, ApprovedAt = DateTime.Now, PostedAt = DateTime.Now };
            _db.InventoryCheckDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var row in group)
            {
                var instance = await FindInstanceAsync(row, cancellationToken);
                var actualBin = await FindBinAsync(warehouse.Id, Value(row, "ActualBinCode"), cancellationToken);
                var current = instance == null ? null : await _db.CurrentItemLocations.AsNoTracking().FirstOrDefaultAsync(x => x.ItemInstanceId == instance.Id, cancellationToken);
                Enum.TryParse<InventoryCheckLineResult>(Value(row, "Result"), true, out var result);
                _db.InventoryCheckLines.Add(new InventoryCheckLine { InventoryCheckDocumentId = document.Id, ItemInstanceId = instance?.Id, SystemBinLocationId = current?.BinLocationId, ActualBinLocationId = actualBin?.Id, Result = result, Note = Value(row, "Note"), CreatedBy = user.UserName });
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ConfirmRepairSendAsync(IReadOnlyCollection<Dictionary<string, string>> rows, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var group in rows.GroupBy(x => new { Vendor = Value(x, "RepairVendorCode"), Reason = Value(x, "Reason") }))
        {
            var vendor = await _db.ExternalParties.FirstAsync(x => x.PartyCode == group.Key.Vendor && x.PartyType == ExternalPartyType.RepairVendor, cancellationToken);
            var document = new RepairDocument { DocumentNo = _documentNumbers.Next("REP", DateTime.Now), DocumentDate = DateTime.Now, RepairVendorId = vendor.Id, Reason = group.Key.Reason, CreatedBy = user.UserName, ApprovedBy = user.UserName, ApprovedAt = DateTime.Now, PostedAt = DateTime.Now };
            _db.RepairDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var row in group)
            {
                var instance = await FindInstanceAsync(row, cancellationToken) ?? throw new InvalidOperationException("Item instance not found.");
                var current = await _db.CurrentItemLocations.FirstAsync(x => x.ItemInstanceId == instance.Id, cancellationToken);
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
                current.UpdatedLocationAt = DateTime.Now;
                current.UpdatedLocationBy = user.UserName;
                if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
                {
                    await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, instance.ItemId, oldStatus, -1, user, cancellationToken);
                }

                AddHistory(instance.Id, MovementActionType.SendToRepair, fromDisplay, $"{vendor.Name} - {targetExternalLocation}", oldStatus, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user);
                AddTransaction(InventoryTransactionType.RepairSend, instance.ItemId, instance.Id, current.WarehouseId, null, -1, ItemStatus.Repairing, nameof(RepairDocument), document.Id, document.DocumentNo, user);
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
        return await _db.Items.FirstOrDefaultAsync(x => x.ItemCode == code && x.IsActive, cancellationToken);
    }

    private async Task<Warehouse?> FindWarehouseAsync(string code, CancellationToken cancellationToken)
    {
        return await _db.Warehouses.FirstOrDefaultAsync(x => x.WarehouseCode == code && x.IsActive, cancellationToken);
    }

    private async Task<BinLocation?> FindBinAsync(int warehouseId, string binCode, CancellationToken cancellationToken)
    {
        return await _db.BinLocations.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.BinCode == binCode && x.IsActive, cancellationToken);
    }

    private async Task<ItemInstance?> FindInstanceAsync(Dictionary<string, string> row, CancellationToken cancellationToken)
    {
        var serial = Value(row, "SerialNumber");
        var barcode = Value(row, "Barcode");
        return await _db.ItemInstances.FirstOrDefaultAsync(x =>
            (!string.IsNullOrWhiteSpace(serial) && x.SerialNumber == serial) ||
            (!string.IsNullOrWhiteSpace(barcode) && x.Barcode == barcode), cancellationToken);
    }

    private async Task ApplyStockDeltaAsync(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var balance = await _db.StockBalances.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status, cancellationToken);
        if (balance == null)
        {
            balance = new StockBalance { WarehouseId = warehouseId, BinLocationId = binLocationId, ItemId = itemId, Status = status, Quantity = 0, CreatedBy = user.UserName };
            _db.StockBalances.Add(balance);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = DateTime.Now;
        balance.UpdatedBy = user.UserName;
    }

    private void AddHistory(int itemInstanceId, MovementActionType action, string? from, string? to, ItemStatus oldStatus, ItemStatus newStatus, string documentType, int documentId, string documentNo, CurrentUserContext user)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory { ItemInstanceId = itemInstanceId, ActionType = action, FromLocationDisplay = from, ToLocationDisplay = to, OldStatus = oldStatus, NewStatus = newStatus, DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo, PerformedAt = DateTime.Now, PerformedBy = user.UserName });
    }

    private void AddTransaction(InventoryTransactionType type, int itemId, int? itemInstanceId, int? warehouseId, int? binLocationId, decimal quantityDelta, ItemStatus statusAfter, string documentType, int documentId, string documentNo, CurrentUserContext user)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction { TransactionType = type, ItemId = itemId, ItemInstanceId = itemInstanceId, WarehouseId = warehouseId, BinLocationId = binLocationId, QuantityDelta = quantityDelta, StatusAfter = statusAfter, DocumentType = documentType, DocumentId = documentId, DocumentNo = documentNo, PostedAt = DateTime.Now, PostedBy = user.UserName });
    }

    private static Dictionary<string, string> Row(ImportBatchRow row)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawJson, JsonOptions) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Value(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Bool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1";
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
        if (!importType.Equals("Inbound", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        AddDuplicateErrors("SerialNumber", "SerialNumber is duplicated in this import file.");
        AddDuplicateErrors("Barcode", "Barcode is duplicated in this import file.");
        AddDuplicateKeyErrors(x => $"{Value(x.Data, "WarehouseCode")}|{Value(x.Data, "BinCode")}", x => !string.IsNullOrWhiteSpace(Value(x.Data, "BinCode")), "BinCode is duplicated in this import file.");
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
                rows.Add(new object?[] { "LAP-DELL-7420", "Dell Latitude 7420", "LAPTOP", "Laptop devices", "PCS", "Piece", "yes", "Laptop Dell Latitude 7420", "Dell Latitude 7420", "Dell 7420" });
                break;
            case "WarehouseStructure":
                rows.Add(new object?[] { "COMP", "Company", "HN", "Ha Noi Branch", "B34", "Warehouse B34", "A", "Zone A", "R01", "Rack 01", "S01", "Shelf 01", "B34_R01_S01" });
                rows.Add(new object?[] { "COMP", "Company", "HN", "Ha Noi Branch", "B34", "Warehouse B34", "A", "Zone A", "R01", "Rack 01", "S02", "Shelf 02", "B34_R01_S02" });
                break;
            case "InventoryCheck":
                rows.Add(new object?[] { "B34", "SN-GB200-0001", "", "B34_R01_S01", "Matched", "Actual bin equals system bin." });
                rows.Add(new object?[] { "B34", "SN-GB200-0002", "", "B34_R01_S02", "WrongLocation", "Item found in another bin." });
                break;
            case "RepairSend":
                rows.Add(new object?[] { "REP01", "SN-GB200-0001", "", "Warranty repair", "2026-05-15", "Vendor workshop shelf A" });
                rows.Add(new object?[] { "REP01", "SN-GB200-0002", "", "Failure analysis", "2026-05-20", "Vendor receiving desk" });
                break;
            default:
                rows.Add(new object?[] { "GB200", "SN-GB200-0001", "BC-GB200-0001", "B34", "B34_R01_S01", "SUP01", "Normal", "Sample inbound row 1" });
                rows.Add(new object?[] { "GB200", "SN-GB200-0002", "BC-GB200-0002", "B34", "B34_R01_S02", "SUP01", "Normal", "Sample inbound row 2" });
                break;
        }

        rows.Add(new object?[] { $"NOTE: Column guide: {TemplateColumnGuide(importType)}" });
        rows.Add(new object?[] { $"NOTE: Prerequisites: {TemplatePrerequisites(importType)}" });
        rows.Add(new object?[] { "NOTE: Delete all NOTE rows before importing real data. The system will ignore NOTE rows but keeping them makes review harder." });
        rows.Add(new object?[] { "NOTE: Required values must match system codes exactly. Enum examples: Result=Matched/Missing/Extra/WrongLocation/Damaged; IsSerialManaged=yes/no." });
        rows.Add(new object?[] { "NOTE: Invalid values include duplicated SerialNumber, duplicated Barcode, occupied BinCode for inbound, missing required codes, or codes outside your warehouse permissions." });
        return rows;
    }

    private static string TemplateColumnGuide(string importType)
    {
        return importType switch
        {
            "ItemMaster" => "ItemCode unique SKU; DefaultName item name; CategoryCode/CategoryName group; UnitCode/UnitName unit; IsSerialManaged yes/no; NameVi/NameEn/NameZh translations.",
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
            "ItemMaster" => new[] { "ItemCode", "DefaultName", "CategoryCode", "UnitCode" },
            "WarehouseStructure" => new[] { "CompanyCode", "BranchCode", "WarehouseCode", "ZoneCode", "RackCode", "ShelfCode", "BinCode" },
            "Inbound" => new[] { "ItemCode", "WarehouseCode", "BinCode" },
            "InventoryCheck" => new[] { "WarehouseCode", "Result" },
            "RepairSend" => new[] { "RepairVendorCode", "Reason", "TargetExternalLocation" },
            _ => Array.Empty<string>()
        };
    }

    private static string NormalizeImportType(string importType)
    {
        return importType.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private static bool CanUseImportType(string importType, CurrentUserContext user)
    {
        return importType is not ("ItemMaster" or "WarehouseStructure") || user.CanManage;
    }

    private static string[] Headers(CurrentUserContext user, params string[] keys)
    {
        return keys.Select(x => ExcelText(user, x)).ToArray();
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
            ["Enum.ItemStatus.InStock"] = "Trong kho",
            ["Enum.ItemStatus.Reserved"] = "Đã giữ chỗ",
            ["Enum.ItemStatus.Repairing"] = "Đang sửa chữa",
            ["Enum.ItemStatus.LentOut"] = "Đã cho mượn",
            ["Enum.ItemStatus.Returned"] = "Đã trả",
            ["Enum.ItemStatus.Damaged"] = "Hư hỏng",
            ["Enum.ItemStatus.Lost"] = "Thất lạc",
            ["Enum.ItemStatus.Disposed"] = "Đã thanh lý",
            ["Enum.ItemStatus.InTransit"] = "Đang vận chuyển",
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
            ["Enum.MovementActionType.Transfer"] = "Điều chuyển"
        },
        ["en"] = new(),
        ["zh"] = new()
        {
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
            ["Enum.ItemStatus.InStock"] = "在库",
            ["Enum.ItemStatus.Reserved"] = "已预留",
            ["Enum.ItemStatus.Repairing"] = "维修中",
            ["Enum.ItemStatus.LentOut"] = "已借出",
            ["Enum.ItemStatus.Returned"] = "已归还",
            ["Enum.ItemStatus.Damaged"] = "损坏",
            ["Enum.ItemStatus.Lost"] = "丢失",
            ["Enum.ItemStatus.Disposed"] = "已报废",
            ["Enum.ItemStatus.InTransit"] = "运输中",
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
            ["Enum.MovementActionType.Transfer"] = "调拨"
        }
    };

    private static readonly Dictionary<string, string[]> ImportHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ItemMaster"] = new[] { "ItemCode", "DefaultName", "CategoryCode", "CategoryName", "UnitCode", "UnitName", "IsSerialManaged", "NameVi", "NameEn", "NameZh" },
        ["WarehouseStructure"] = new[] { "CompanyCode", "CompanyName", "BranchCode", "BranchName", "WarehouseCode", "WarehouseName", "ZoneCode", "ZoneName", "RackCode", "RackName", "ShelfCode", "ShelfName", "BinCode" },
        ["Inbound"] = new[] { "ItemCode", "SerialNumber", "Barcode", "WarehouseCode", "BinCode", "SourcePartyCode", "Condition", "Note" },
        ["InventoryCheck"] = new[] { "WarehouseCode", "SerialNumber", "Barcode", "ActualBinCode", "Result", "Note" },
        ["RepairSend"] = new[] { "RepairVendorCode", "SerialNumber", "Barcode", "Reason", "ExpectedReturnDate", "TargetExternalLocation" }
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
