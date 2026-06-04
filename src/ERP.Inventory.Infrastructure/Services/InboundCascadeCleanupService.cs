using ERP.Inventory.Application.Common;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class InboundCascadeCleanupRequest
{
    public int InboundDocumentId { get; init; }
    public IReadOnlyCollection<int> RemovedLineIds { get; init; } = Array.Empty<int>();
    public IReadOnlyCollection<int> ItemInstanceIds { get; init; } = Array.Empty<int>();
    public bool DryRun { get; init; } = true;
}

public sealed class InboundCascadeCleanupReport
{
    public bool HasDependencies => Reasons.Count > 0;
    public List<string> Reasons { get; } = new();
    public List<string> AffectedDocuments { get; } = new();
    public List<string> AffectedTables { get; } = new();
}

public interface IInboundCascadeCleanupService
{
    Task<InboundCascadeCleanupReport> PreviewAsync(InboundCascadeCleanupRequest request, CancellationToken cancellationToken = default);
    Task<InboundCascadeCleanupReport> CleanupAsync(InboundCascadeCleanupRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public sealed class InboundCascadeCleanupService : IInboundCascadeCleanupService
{
    private readonly InventoryDbContext _db;

    public InboundCascadeCleanupService(InventoryDbContext db)
    {
        _db = db;
    }

    public Task<InboundCascadeCleanupReport> PreviewAsync(InboundCascadeCleanupRequest request, CancellationToken cancellationToken = default)
        => BuildReportAsync(request, cancellationToken);

    public async Task<InboundCascadeCleanupReport> CleanupAsync(InboundCascadeCleanupRequest request, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(request, cancellationToken);
        if (!report.HasDependencies)
        {
            return report;
        }

        var ids = request.ItemInstanceIds.Distinct().ToArray();
        var ownsTransaction = _db.Database.CurrentTransaction == null;
        await using var tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;

        _db.InventoryCheckLines.RemoveRange(_db.InventoryCheckLines.Where(x => x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value)));
        _db.BorrowDocumentLogs.RemoveRange(_db.BorrowDocumentLogs.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.BorrowDocumentLines.RemoveRange(_db.BorrowDocumentLines.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.RepairDocumentLogs.RemoveRange(_db.RepairDocumentLogs.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.RepairDocumentLines.RemoveRange(_db.RepairDocumentLines.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.MoveDocumentLines.RemoveRange(_db.MoveDocumentLines.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.AdjustmentDocumentLogs.RemoveRange(_db.AdjustmentDocumentLogs.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.AdjustmentDocumentLines.RemoveRange(_db.AdjustmentDocumentLines.Where(x => ids.Contains(x.ItemInstanceId)));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => ids.Contains(x.ItemInstanceId) && !(x.DocumentType == nameof(InboundDocument) && x.DocumentId == request.InboundDocumentId)));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value) && !(x.DocumentType == nameof(InboundDocument) && x.DocumentId == request.InboundDocumentId)));

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = "InboundCascadeCleanup",
            EntityName = nameof(InboundDocument),
            EntityId = request.InboundDocumentId,
            Result = "Success",
            AfterJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.InboundDocumentId,
                request.RemovedLineIds,
                request.ItemInstanceIds,
                report.AffectedDocuments,
                report.AffectedTables
            }),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        if (ownsTransaction && tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return report;
    }

    private async Task<InboundCascadeCleanupReport> BuildReportAsync(InboundCascadeCleanupRequest request, CancellationToken cancellationToken)
    {
        var report = new InboundCascadeCleanupReport();
        var ids = request.ItemInstanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return report;
        }

        var serials = await _db.ItemInstances
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ItemInstanceKey(x.Id, x.SerialNumber ?? string.Empty, x.Item != null ? x.Item.ItemCode : string.Empty))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        await AddLineDependenciesAsync(
            report,
            "InventoryCheckLines",
            await _db.InventoryCheckLines.AsNoTracking()
                .Where(x => x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value))
                .Select(x => new DependencyHit(x.ItemInstanceId!.Value, "inventory-check", x.InventoryCheckDocumentId, x.InventoryCheckDocument != null ? x.InventoryCheckDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials,
            "Delete or rebuild the inventory check document first.");

        await AddLineDependenciesAsync(
            report,
            "BorrowDocumentLines",
            await _db.BorrowDocumentLines.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "borrow", x.BorrowDocumentId, x.BorrowDocument != null ? x.BorrowDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials,
            "Delete borrow return/lend lines before removing the inbound item.");

        await AddLogDependenciesAsync(
            report,
            "BorrowDocumentLogs",
            await _db.BorrowDocumentLogs.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "borrow-log", x.BorrowDocumentId, x.BorrowDocument != null ? x.BorrowDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials);

        await AddLineDependenciesAsync(
            report,
            "RepairDocumentLines",
            await _db.RepairDocumentLines.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "repair", x.RepairDocumentId, x.RepairDocument != null ? x.RepairDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials,
            "Delete repair send/receive lines before removing the inbound item.");

        await AddLogDependenciesAsync(
            report,
            "RepairDocumentLogs",
            await _db.RepairDocumentLogs.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "repair-log", x.RepairDocumentId, x.RepairDocument != null ? x.RepairDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials);

        await AddLineDependenciesAsync(
            report,
            "MoveDocumentLines",
            await _db.MoveDocumentLines.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "move", x.MoveDocumentId, x.MoveDocument != null ? x.MoveDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials,
            "Delete move lines before removing the inbound item.");

        await AddLineDependenciesAsync(
            report,
            "AdjustmentDocumentLines",
            await _db.AdjustmentDocumentLines.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "adjustment", x.AdjustmentDocumentId, x.AdjustmentDocument != null ? x.AdjustmentDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials,
            "Delete adjustment lines before removing the inbound item.");

        await AddLogDependenciesAsync(
            report,
            "AdjustmentDocumentLogs",
            await _db.AdjustmentDocumentLogs.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId))
                .Select(x => new DependencyHit(x.ItemInstanceId, "adjustment-log", x.AdjustmentDocumentId, x.AdjustmentDocument != null ? x.AdjustmentDocument.DocumentNo : string.Empty))
                .ToListAsync(cancellationToken),
            serials);

        await AddLogDependenciesAsync(
            report,
            "ItemMovementHistories",
            await _db.ItemMovementHistories.AsNoTracking()
                .Where(x => ids.Contains(x.ItemInstanceId) && !(x.DocumentType == nameof(InboundDocument) && x.DocumentId == request.InboundDocumentId))
                .Select(x => new DependencyHit(x.ItemInstanceId, x.DocumentType, x.DocumentId, x.DocumentNo))
                .ToListAsync(cancellationToken),
            serials);

        await AddLogDependenciesAsync(
            report,
            "InventoryTransactions",
            await _db.InventoryTransactions.AsNoTracking()
                .Where(x => x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value) && !(x.DocumentType == nameof(InboundDocument) && x.DocumentId == request.InboundDocumentId))
                .Select(x => new DependencyHit(x.ItemInstanceId!.Value, x.DocumentType, x.DocumentId, x.DocumentNo))
                .ToListAsync(cancellationToken),
            serials);

        return report;
    }

    private static Task AddLineDependenciesAsync(
        InboundCascadeCleanupReport report,
        string table,
        IReadOnlyCollection<DependencyHit> hits,
        IReadOnlyDictionary<int, ItemInstanceKey> serials,
        string guidance)
    {
        AddHits(report, table, hits, serials, guidance);
        return Task.CompletedTask;
    }

    private static Task AddLogDependenciesAsync(
        InboundCascadeCleanupReport report,
        string table,
        IReadOnlyCollection<DependencyHit> hits,
        IReadOnlyDictionary<int, ItemInstanceKey> serials)
    {
        AddHits(report, table, hits, serials, "Delete or rebuild downstream history before removing the inbound item.");
        return Task.CompletedTask;
    }

    private static void AddHits(
        InboundCascadeCleanupReport report,
        string table,
        IReadOnlyCollection<DependencyHit> hits,
        IReadOnlyDictionary<int, ItemInstanceKey> serials,
        string guidance)
    {
        if (hits.Count == 0) return;
        report.AffectedTables.Add(table);
        foreach (var hit in hits.Distinct())
        {
            serials.TryGetValue(hit.ItemInstanceId, out var item);
            var itemText = item == null
                ? hit.ItemInstanceId.ToString()
                : $"{item.ItemCode}/{item.SerialNumber}";
            var document = string.IsNullOrWhiteSpace(hit.DocumentNo) ? $"{hit.DocumentType}#{hit.DocumentId}" : hit.DocumentNo;
            report.AffectedDocuments.Add($"{hit.DocumentType}:{document}");
            report.Reasons.Add($"Item {itemText} is referenced by {hit.DocumentType} document {document}. {guidance}");
        }
    }

    private sealed record DependencyHit(int ItemInstanceId, string DocumentType, int DocumentId, string DocumentNo);
    private sealed record ItemInstanceKey(int Id, string SerialNumber, string ItemCode);
}
