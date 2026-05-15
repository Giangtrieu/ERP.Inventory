using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Web.Controllers;

/// <summary>AuditLogs, ImportBatches, SystemSummary</summary>
[Route("[controller]")]
public sealed class SystemController : ManagementBaseController
{
    public SystemController(InventoryDbContext db, ICurrentUserService currentUserService)
        : base(db, currentUserService) { }

    [HttpGet("Summary")]
    [Authorize(Roles = "Admin,Warehouse Manager")]
    public async Task<IActionResult> SystemSummary(CancellationToken cancellationToken)
    {
        var result = new
        {
            users = await Db.SystemUsers.CountAsync(cancellationToken),
            roles = await Db.SystemRoles.CountAsync(cancellationToken),
            warehousePermissions = await Db.UserWarehousePermissions.CountAsync(cancellationToken),
            unreadNotifications = await Db.Notifications.CountAsync(x => !x.IsRead, cancellationToken)
        };
        return Json(result);
    }

    [HttpGet("AuditLogs")]
    public async Task<IActionResult> AuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] string? keyword = null, [FromQuery] string? userName = null, [FromQuery] string? action = null, [FromQuery] string? entityName = null, [FromQuery] string? referenceNo = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        if (pageSize != 0) pageSize = Math.Clamp(pageSize, 1, 500);
        var query = Db.AuditLogs.AsNoTracking().AsQueryable();
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value);
        if (toDate.HasValue) { var to = toDate.Value.Date.AddDays(1); query = query.Where(x => x.CreatedAt < to); }
        if (!string.IsNullOrWhiteSpace(keyword)) { var key = keyword.Trim(); query = query.Where(x => x.UserName.Contains(key) || x.Action.Contains(key) || x.EntityName.Contains(key) || (x.ReferenceNo != null && x.ReferenceNo.Contains(key))); }
        if (!string.IsNullOrWhiteSpace(userName)) { var key = userName.Trim(); query = query.Where(x => x.UserName.Contains(key)); }
        if (!string.IsNullOrWhiteSpace(action)) { var key = action.Trim(); query = query.Where(x => x.Action.Contains(key)); }
        if (!string.IsNullOrWhiteSpace(entityName)) { var key = entityName.Trim(); query = query.Where(x => x.EntityName.Contains(key)); }
        if (!string.IsNullOrWhiteSpace(referenceNo)) { var key = referenceNo.Trim(); query = query.Where(x => x.ReferenceNo != null && x.ReferenceNo.Contains(key)); }

        var total = await query.CountAsync(cancellationToken);
        if (pageSize == 0)
            pageSize = total == 0 ? 1 : total;
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.CreatedAt, x.UserName, x.Action, x.EntityName, x.ReferenceNo, x.Result })
            .ToArrayAsync(cancellationToken);
        return Json(new PagedResult<object> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("ImportBatches")]
    public async Task<IActionResult> ImportBatches(CancellationToken cancellationToken)
    {
        var rows = await Db.ImportBatchRows.AsNoTracking()
            .Include(x => x.ImportBatch)
            .Where(x => !x.IsValid || x.Severity != ValidationSeverity.Info)
            .OrderByDescending(x => x.ImportBatch != null ? x.ImportBatch.CreatedAt : x.CreatedAt)
            .Take(100)
            .Select(x => new
            {
                batchNo = x.ImportBatch != null ? x.ImportBatch.BatchNo : string.Empty,
                row = x.RowNumber, column = x.ColumnName, severity = x.Severity,
                message = x.Message, suggestedFix = x.SuggestedFix
            })
            .ToArrayAsync(cancellationToken);
        return Json(rows);
    }

    /// <summary>
    /// Migrate dữ liệu cũ từ BorrowDocumentLine và InboundDocumentLine vào BorrowDocumentLogs / InboundDocumentLogs.
    /// Idempotent — bỏ qua document đã có log.
    /// </summary>
    [HttpPost("MigrateDocumentLogs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateDocumentLogs(CancellationToken cancellationToken)
    {
        int borrowCreated = 0, inboundCreated = 0, adjustCreated = 0;

        // ─── Migrate BorrowDocument ───────────────────────────────────────────────
        var borrowDocs = await Db.BorrowDocuments.AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)
            .Include(x => x.Lines).ThenInclude(x => x.FromBinLocation)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .ToListAsync(cancellationToken);

        var existingBorrowDocIds = await Db.BorrowDocumentLogs
            .Select(x => x.BorrowDocumentId).Distinct().ToListAsync(cancellationToken);
        var existingBorrowSet = new HashSet<int>(existingBorrowDocIds);

        foreach (var doc in borrowDocs)
        {
            if (existingBorrowSet.Contains(doc.Id)) continue;
            foreach (var line in doc.Lines)
            {
                // BorrowIssue log
                Db.BorrowDocumentLogs.Add(new ERP.Inventory.Domain.Entities.BorrowDocumentLog
                {
                    BorrowDocumentId = doc.Id,
                    ItemInstanceId = line.ItemInstanceId,
                    Action = "BorrowIssue",
                    OldStatus = "Normal",
                    NewStatus = "LentOut",
                    Borrower = doc.Borrower != null ? $"{doc.Borrower.PartyCode}-{doc.Borrower.Name}" : null,
                    BorrowDepartment = doc.BorrowDepartment,
                    BorrowerPhone = doc.BorrowerPhone,
                    DepartmentOwner = doc.ApprovedBy,
                    OldLocationText = line.FromBinLocationId.HasValue ? line.FromBinLocation?.BinCode ?? $"Bin#{line.FromBinLocationId}" : "Unknown",
                    NewLocationText = line.TargetExternalLocation ?? "External",
                    PerformedBy = doc.CreatedBy,
                    Timestamp = doc.PostedAt,
                    Note = line.Note
                });
                borrowCreated++;

                // BorrowReturn log nếu đã trả
                if (line.IsReturned)
                {
                    var returnedAt = line.ReturnedAt ?? doc.PostedAt.AddMinutes(1);
                    var newStatus = line.ReturnCondition.HasValue ? line.ReturnCondition.Value.ToString() : "Normal";
                    Db.BorrowDocumentLogs.Add(new ERP.Inventory.Domain.Entities.BorrowDocumentLog
                    {
                        BorrowDocumentId = doc.Id,
                        ItemInstanceId = line.ItemInstanceId,
                        Action = "BorrowReturn",
                        OldStatus = "LentOut",
                        NewStatus = newStatus,
                        Borrower = doc.Borrower != null ? $"{doc.Borrower.PartyCode}-{doc.Borrower.Name}" : null,
                        BorrowDepartment = doc.BorrowDepartment,
                        BorrowerPhone = doc.BorrowerPhone,
                        DepartmentOwner = doc.ApprovedBy,
                        OldLocationText = line.TargetExternalLocation ?? "External",
                        NewLocationText = line.TargetBinLocationId.HasValue ? line.TargetBinLocation?.BinCode ?? $"Bin#{line.TargetBinLocationId}" : "Warehouse",
                        PerformedBy = doc.CreatedBy,
                        Timestamp = returnedAt,
                        Note = line.Note
                    });
                    borrowCreated++;
                }
            }
        }

        // ─── Migrate InboundDocument ──────────────────────────────────────────────
        var inboundDocs = await Db.InboundDocuments.AsNoTracking()
            .Include(x => x.Receiver)
            .Include(x => x.Lines).ThenInclude(x => x.BinLocation)
            .ToListAsync(cancellationToken);

        var existingInboundDocIds = await Db.InboundDocumentLogs
            .Select(x => x.InboundDocumentId).Distinct().ToListAsync(cancellationToken);
        var existingInboundSet = new HashSet<int>(existingInboundDocIds);

        foreach (var doc in inboundDocs)
        {
            if (existingInboundSet.Contains(doc.Id)) continue;
            foreach (var line in doc.Lines)
            {
                if (!line.ItemInstanceId.HasValue) continue;
                Db.InboundDocumentLogs.Add(new ERP.Inventory.Domain.Entities.InboundDocumentLog
                {
                    InboundDocumentId = doc.Id,
                    ItemInstanceId = line.ItemInstanceId.Value,
                    Action = "InboundReceive",
                    OldStatus = "Reserved",
                    NewStatus = line.Condition ?? "Normal",
                    Receiver = doc.Receiver != null ? $"{doc.Receiver.PartyCode}-{doc.Receiver.Name}" : null,
                    ReceiverPhone = doc.PartyPhone,
                    ReceiverDepartment = doc.PartyDepartment,
                    DepartmentOwner = doc.DepartmentOwner,
                    OldLocationText = "Supplier",
                    NewLocationText = line.BinLocation?.FullPath ?? line.BinLocation?.BinCode ?? $"Bin#{line.BinLocationId}",
                    PerformedBy = doc.CreatedBy,
                    Timestamp = doc.PostedAt,
                    Note = line.Note
                });
                inboundCreated++;
            }
        }

        var adjustDocs = await Db.AdjustmentDocuments.AsNoTracking()
           .Include(x => x.Lines).ToListAsync(cancellationToken);

        var existingAdjustDocIds = await Db.AdjustmentDocumentLogs
            .Select(x => x.AdjustmentDocumentId).Distinct().ToListAsync(cancellationToken);
        var existingAdjustSet = new HashSet<int>(existingAdjustDocIds);

        foreach (var doc in adjustDocs)
        {
            if (existingAdjustSet.Contains(doc.Id)) continue;
            foreach (var line in doc.Lines)
            {
                if (line.ItemInstanceId == 0) continue;
                var itemHistory = await Db.ItemMovementHistories.FirstOrDefaultAsync(x => x.DocumentId == doc.Id && x.ItemInstanceId == line.ItemInstanceId && x.ActionType == MovementActionType.Adjustment);
                if (itemHistory == null) continue;
                Db.AdjustmentDocumentLogs.Add(new ERP.Inventory.Domain.Entities.AdjustmentDocumentLog
                {
                    AdjustmentDocumentId = doc.Id,
                    ItemInstanceId = line.ItemInstanceId,
                    Action = "Adjust",
                    OldStatus = itemHistory.OldStatus.ToString(),
                    NewStatus = itemHistory.NewStatus.ToString() ?? "Normal",
                    OldLocationText = itemHistory.FromLocationDisplay,
                    NewLocationText = itemHistory.ToLocationDisplay,
                    Reason = line.Reason,
                    PerformedBy = doc.CreatedBy,
                    Timestamp = doc.PostedAt
                });
                adjustCreated++;
            }
        }

        await Db.SaveChangesAsync(cancellationToken);

        return Json(new { success = true, borrowCreated, inboundCreated, message = $"Migration complete: {borrowCreated} borrow logs, {inboundCreated} inbound logs created, {adjustCreated} adjustment logs created." });
    }
}
