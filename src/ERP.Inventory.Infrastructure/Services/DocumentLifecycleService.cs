using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Entities;
using ERP.Inventory.Domain.Enums;
using ERP.Inventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ERP.Inventory.Infrastructure.Services;

public sealed class DocumentLifecycleService : IDocumentLifecycleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly InventoryDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IInboundService _inboundService;
    private readonly IInventoryOperationService _moveService;
    private readonly IRepairService _repairService;
    private readonly IBorrowService _borrowService;
    private readonly IQuantityInventoryService _quantityService;
    private readonly AdjustmentService _adjustmentService;

    public DocumentLifecycleService(
        InventoryDbContext db,
        IDateTimeProvider clock,
        IInboundService inboundService,
        IInventoryOperationService moveService,
        IRepairService repairService,
        IBorrowService borrowService,
        IQuantityInventoryService quantityService,
        AdjustmentService adjustmentService)
    {
        _db = db;
        _clock = clock;
        _inboundService = inboundService;
        _moveService = moveService;
        _repairService = repairService;
        _borrowService = borrowService;
        _quantityService = quantityService;
        _adjustmentService = adjustmentService;
    }

    public async Task<ServiceResult<DocumentMutationResultDto>> DeleteAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeType(type);
        if (normalizedType == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.");
        }

        await using var tx = await BeginLifecycleTransactionAsync(cancellationToken);
        try
        {
            var result = await DeleteCoreAsync(normalizedType, id, user, cancellationToken);
            if (!result.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return result;
            }

            if (result.Data != null)
            {
                AddAuditLog(user, "Reverse", EntityNameForType(normalizedType), result.Data.DocumentId, result.Data.DocumentNo, "Document effects reversed before delete.");
                AddAuditLog(user, "Delete", EntityNameForType(normalizedType), result.Data.DocumentId, result.Data.DocumentNo, "Document deleted.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<DocumentMutationResultDto>> EditAsync(string type, int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeType(type);
        if (normalizedType == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.");
        }

        await using var tx = await BeginLifecycleTransactionAsync(cancellationToken);
        try
        {
            var result = await EditInternalAsync(normalizedType, id, payload, user, cancellationToken);
            if (!result.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return result;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<DocumentMutationResultDto>> RebuildAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeType(type);
        if (normalizedType == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.");
        }

        await using var tx = await BeginLifecycleTransactionAsync(cancellationToken);
        try
        {
            var editModel = await GetEditModelAsync(normalizedType, id, user, cancellationToken);
            if (!editModel.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<DocumentMutationResultDto>.Fail(editModel.Errors);
            }

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(editModel.Data!.Payload, JsonOptions));
            var result = await EditInternalAsync(normalizedType, id, document.RootElement.Clone(), user, cancellationToken, editModel.Data.DocumentNo);
            if (!result.Success)
            {
                await tx.RollbackAsync(cancellationToken);
                return result;
            }

            if (result.Data != null)
            {
                AddAuditLog(user, "Rebuild", EntityNameForType(normalizedType), result.Data.DocumentId, result.Data.DocumentNo, "Rebuild Effects", JsonSerializer.Serialize(editModel.Data.Payload, JsonOptions));
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<DocumentDependencyDto>> PreviewDependenciesAsync(string type, int id, string action, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeType(type);
        if (normalizedType == null)
        {
            return ServiceResult<DocumentDependencyDto>.Fail($"Unsupported document type '{type}'.");
        }

        var documentNo = await GetDocumentNoAsync(normalizedType, id, cancellationToken);
        if (documentNo == null)
        {
            return ServiceResult<DocumentDependencyDto>.Fail("Document not found.");
        }

        var reasons = await GetDependencyReasonsAsync(normalizedType, id, cancellationToken);
        return ServiceResult<DocumentDependencyDto>.Ok(new DocumentDependencyDto
        {
            Action = action,
            CanProceed = reasons.Count == 0,
            DocumentId = id,
            DocumentNo = documentNo,
            DocumentType = normalizedType,
            Reasons = reasons
        }, reasons.Count == 0 ? "No blocking dependency found." : "Document has blocking dependencies.");
    }

    public async Task<ServiceResult<DocumentEditModelDto>> GetEditModelAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeType(type);
        if (normalizedType == null)
        {
            return ServiceResult<DocumentEditModelDto>.Fail($"Unsupported document type '{type}'.");
        }

        var payload = normalizedType switch
        {
            "inbound" => await BuildInboundPayloadAsync(id, cancellationToken),
            "move" => await BuildMovePayloadAsync(id, cancellationToken),
            "adjustment" => await BuildAdjustmentPayloadAsync(id, cancellationToken),
            "borrow-lend" => await BuildBorrowLendPayloadAsync(id, cancellationToken),
            "borrow-return" => await BuildBorrowReturnPayloadAsync(id, cancellationToken),
            "repair-send" => await BuildRepairSendPayloadAsync(id, cancellationToken),
            "repair-receive" => await BuildRepairReceivePayloadAsync(id, cancellationToken),
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await BuildQuantityPayloadAsync(id, cancellationToken),
            _ => null
        };

        if (payload == null)
        {
            return ServiceResult<DocumentEditModelDto>.Fail("Document not found.");
        }

        var documentNo = await GetDocumentNoAsync(normalizedType, id, cancellationToken) ?? string.Empty;
        var audit = await BuildAuditTrailAsync(EntityNameForType(normalizedType), id, documentNo, cancellationToken);

        return ServiceResult<DocumentEditModelDto>.Ok(new DocumentEditModelDto
        {
            DocumentId = id,
            DocumentNo = documentNo,
            DocumentType = normalizedType,
            Payload = payload,
            Audit = audit
        });
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditInternalAsync(string type, int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken, string? preservedDocumentNo = null)
    {
        return type switch
        {
            "inbound" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "move" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "adjustment" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "borrow-lend" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "repair-send" => await RebuildStandaloneAsync(id, type, payload, user, cancellationToken, preservedDocumentNo),
            "borrow-return" => await RebuildBorrowReturnAsync(id, payload, user, cancellationToken),
            "repair-receive" => await RebuildRepairReceiveAsync(id, payload, user, cancellationToken),
            _ => ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.")
        };
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> RebuildStandaloneAsync(int id, string type, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken, string? preservedDocumentNo = null)
    {
        var documentNo = preservedDocumentNo ?? await GetDocumentNoAsync(type, id, cancellationToken);
        if (documentNo == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Document not found.");
        }

        var deleteResult = await DeleteCoreAsync(type, id, user, cancellationToken);
        if (!deleteResult.Success)
        {
            return deleteResult;
        }

        var patchedPayload = ForceDocumentNo(payload, documentNo);
        var postResult = await RepostAsync(type, patchedPayload, user, cancellationToken);
        if (!postResult.Success || postResult.Data == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(postResult.Errors);
        }

        AddAuditLog(user, "Edit", EntityNameForType(type), postResult.Data.DocumentId, postResult.Data.DocumentNo, "Document edited and effects rebuilt.", payload.GetRawText());

        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Edit",
            DocumentId = postResult.Data.DocumentId,
            DocumentNo = postResult.Data.DocumentNo,
            DocumentType = type,
            ProcessedAt = _clock.UtcNow
        }, "Document updated.");
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> RebuildBorrowReturnAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var doc = await _db.BorrowDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (doc == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");
        }

        var deleteResult = await DeleteBorrowReturnAsync(id, user, cancellationToken);
        if (!deleteResult.Success)
        {
            return deleteResult;
        }

        var request = JsonSerializer.Deserialize<BorrowReturnRequest>(payload.GetRawText(), JsonOptions);
        if (request == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Invalid borrow return payload.");
        }

        var patched = JsonNode.Parse(payload.GetRawText())?.AsObject() ?? new JsonObject();
        patched["borrowDocumentId"] = id;
        patched["borrowDocumentNo"] = doc.DocumentNo;
        request = patched.Deserialize<BorrowReturnRequest>(JsonOptions);
        if (request == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Invalid borrow return payload.");
        }

        var postResult = await _borrowService.ReturnAsync(request, user, cancellationToken);
        if (!postResult.Success || postResult.Data == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(postResult.Errors);
        }

        AddAuditLog(user, "Edit", nameof(BorrowDocument), postResult.Data.DocumentId, postResult.Data.DocumentNo, "Borrow return edited and effects rebuilt.", payload.GetRawText());

        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Edit",
            DocumentId = postResult.Data.DocumentId,
            DocumentNo = postResult.Data.DocumentNo,
            DocumentType = "borrow-return",
            ProcessedAt = _clock.UtcNow
        }, "Borrow return updated.");
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> RebuildRepairReceiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var doc = await _db.RepairDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (doc == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");
        }

        var deleteResult = await DeleteRepairReceiveAsync(id, user, cancellationToken);
        if (!deleteResult.Success)
        {
            return deleteResult;
        }

        var patched = JsonNode.Parse(payload.GetRawText())?.AsObject() ?? new JsonObject();
        patched["repairDocumentId"] = id;
        patched["repairDocumentNo"] = doc.DocumentNo;
        var request = patched.Deserialize<RepairReceiveRequest>(JsonOptions);
        if (request == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Invalid repair receive payload.");
        }

        var postResult = await _repairService.ReceiveFromRepairAsync(request, user, cancellationToken);
        if (!postResult.Success || postResult.Data == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(postResult.Errors);
        }

        AddAuditLog(user, "Edit", nameof(RepairDocument), postResult.Data.DocumentId, postResult.Data.DocumentNo, "Repair receive edited and effects rebuilt.", payload.GetRawText());

        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Edit",
            DocumentId = postResult.Data.DocumentId,
            DocumentNo = postResult.Data.DocumentNo,
            DocumentType = "repair-receive",
            ProcessedAt = _clock.UtcNow
        }, "Repair receive updated.");
    }

    private async Task<ServiceResult<PostedDocumentDto>> RepostAsync(string type, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "inbound":
            {
                var request = JsonSerializer.Deserialize<InboundRequest>(payload.GetRawText(), JsonOptions);
                return request == null
                    ? ServiceResult<PostedDocumentDto>.Fail("Invalid inbound payload.")
                    : await _inboundService.CreateInboundAsync(request, user, cancellationToken);
            }
            case "move":
            {
                var request = JsonSerializer.Deserialize<MoveLocationRequest>(payload.GetRawText(), JsonOptions);
                return request == null
                    ? ServiceResult<PostedDocumentDto>.Fail("Invalid move payload.")
                    : await _moveService.MoveLocationAsync(request, user, cancellationToken);
            }
            case "adjustment":
            {
                var request = JsonSerializer.Deserialize<AdjustmentRequest>(payload.GetRawText(), JsonOptions);
                return request == null
                    ? ServiceResult<PostedDocumentDto>.Fail("Invalid adjustment payload.")
                    : await _adjustmentService.AdjustAsync(request, user, cancellationToken);
            }
            case "borrow-lend":
            {
                var request = JsonSerializer.Deserialize<BorrowLendRequest>(payload.GetRawText(), JsonOptions);
                return request == null
                    ? ServiceResult<PostedDocumentDto>.Fail("Invalid borrow lend payload.")
                    : await _borrowService.LendAsync(request, user, cancellationToken);
            }
            case "repair-send":
            {
                var request = JsonSerializer.Deserialize<RepairSendRequest>(payload.GetRawText(), JsonOptions);
                return request == null
                    ? ServiceResult<PostedDocumentDto>.Fail("Invalid repair send payload.")
                    : await _repairService.SendToRepairAsync(request, user, cancellationToken);
            }
            case "quantity-receive":
            case "quantity-issue":
            case "quantity-adjust":
            {
                var request = JsonSerializer.Deserialize<QuantityInventoryRequest>(payload.GetRawText(), JsonOptions);
                if (request == null)
                {
                    return ServiceResult<PostedDocumentDto>.Fail("Invalid quantity inventory payload.");
                }

                return type switch
                {
                    "quantity-receive" => await _quantityService.ReceiveAsync(request, user, cancellationToken, true),
                    "quantity-issue" => await _quantityService.IssueAsync(request, user, cancellationToken, true),
                    _ => await _quantityService.AdjustAsync(request, user, cancellationToken, true)
                };
            }
            default:
                return ServiceResult<PostedDocumentDto>.Fail($"Unsupported document type '{type}'.");
        }
    }

    private Task<ServiceResult<DocumentMutationResultDto>> DeleteCoreAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        return type switch
        {
            "inbound" => DeleteInboundAsync(id, user, cancellationToken),
            "move" => DeleteMoveAsync(id, user, cancellationToken),
            "adjustment" => DeleteAdjustmentAsync(id, user, cancellationToken),
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => DeleteQuantityAsync(id, type, user, cancellationToken),
            "borrow-lend" => DeleteBorrowLendAsync(id, user, cancellationToken),
            "borrow-return" => DeleteBorrowReturnAsync(id, user, cancellationToken),
            "repair-send" => DeleteRepairSendAsync(id, user, cancellationToken),
            "repair-receive" => DeleteRepairReceiveAsync(id, user, cancellationToken),
            _ => Task.FromResult(ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.")),
        };
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteInboundAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.InboundDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Inbound document not found.");
        }

        var instanceIds = document.Lines.Where(x => x.ItemInstanceId.HasValue).Select(x => x.ItemInstanceId!.Value).Distinct().ToArray();
        var laterDeps = await FindLaterHistoryDependenciesAsync(nameof(InboundDocument), id, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        var itemIds = document.Lines.Select(x => x.ItemId).Distinct().ToArray();
        _db.InboundDocumentLogs.RemoveRange(_db.InboundDocumentLogs.Where(x => x.InboundDocumentId == id));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(InboundDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(InboundDocument) && x.DocumentId == id));
        _db.CurrentItemLocations.RemoveRange(_db.CurrentItemLocations.Where(x => instanceIds.Contains(x.ItemInstanceId)));
        _db.InboundDocumentLines.RemoveRange(document.Lines);
        _db.ItemInstances.RemoveRange(_db.ItemInstances.Where(x => instanceIds.Contains(x.Id)));
        _db.InboundDocuments.Remove(document);
        await CleanupPostSideEffectsAsync(nameof(InboundDocument), id, document.DocumentNo, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("inbound", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteMoveAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.MoveDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Move document not found.");
        }

        var instanceIds = document.Lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterHistoryDependenciesAsync(nameof(MoveDocument), id, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(MoveDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(MoveDocument) && x.DocumentId == id));
        _db.MoveDocumentLines.RemoveRange(document.Lines);
        _db.MoveDocuments.Remove(document);
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await CleanupPostSideEffectsAsync(nameof(MoveDocument), id, document.DocumentNo, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("move", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteAdjustmentAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.AdjustmentDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Adjustment document not found.");
        }

        var instanceIds = document.Lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterHistoryDependenciesAsync(nameof(AdjustmentDocument), id, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.AdjustmentDocumentLogs.RemoveRange(_db.AdjustmentDocumentLogs.Where(x => x.AdjustmentDocumentId == id));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == id));
        _db.AdjustmentDocumentLines.RemoveRange(document.Lines);
        _db.AdjustmentDocuments.Remove(document);
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await CleanupPostSideEffectsAsync(nameof(AdjustmentDocument), id, document.DocumentNo, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("adjustment", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteBorrowLendAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.BorrowDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");
        }

        if (document.Lines.Any(x => x.IsReturned))
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Cannot delete borrow lend after return has been posted. Delete borrow return first.");
        }

        var instanceIds = document.Lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterHistoryDependenciesAsync(nameof(BorrowDocument), id, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.BorrowDocumentLogs.RemoveRange(_db.BorrowDocumentLogs.Where(x => x.BorrowDocumentId == id));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(BorrowDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(BorrowDocument) && x.DocumentId == id));
        _db.BorrowDocumentLines.RemoveRange(document.Lines);
        _db.BorrowDocuments.Remove(document);
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await CleanupPostSideEffectsAsync(nameof(BorrowDocument), id, document.DocumentNo, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("borrow-lend", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteBorrowReturnAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.BorrowDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");
        }

        var returnedLines = document.Lines.Where(x => x.IsReturned).ToArray();
        if (returnedLines.Length == 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document has no return effects to delete.");
        }

        var instanceIds = returnedLines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterPhaseDependenciesAsync(nameof(BorrowDocument), id, MovementActionType.ReturnBorrowed, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        foreach (var line in returnedLines)
        {
            line.IsReturned = false;
            line.ReturnCondition = null;
            line.ReturnedAt = null;
            line.TargetBinLocationId = null;
            line.UpdatedAt = _clock.UtcNow;
            line.UpdatedBy = user.UserName;
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.BorrowDocumentLogs.RemoveRange(_db.BorrowDocumentLogs.Where(x => x.BorrowDocumentId == id && x.Action == "BorrowReturn"));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(BorrowDocument) && x.DocumentId == id && x.TransactionType == InventoryTransactionType.BorrowReturn));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(BorrowDocument) && x.DocumentId == id && x.ActionType == MovementActionType.ReturnBorrowed));
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("borrow-return", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteRepairSendAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.RepairDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");
        }

        if (document.Lines.Any(x => x.IsReturned))
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Cannot delete repair send after repair receive has been posted. Delete repair receive first.");
        }

        var instanceIds = document.Lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterHistoryDependenciesAsync(nameof(RepairDocument), id, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.RepairDocumentLogs.RemoveRange(_db.RepairDocumentLogs.Where(x => x.RepairDocumentId == id));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(RepairDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(RepairDocument) && x.DocumentId == id));
        _db.RepairDocumentLines.RemoveRange(document.Lines);
        _db.RepairDocuments.Remove(document);
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await CleanupPostSideEffectsAsync(nameof(RepairDocument), id, document.DocumentNo, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("repair-send", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteRepairReceiveAsync(int id, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.RepairDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");
        }

        var returnedLines = document.Lines.Where(x => x.IsReturned).ToArray();
        if (returnedLines.Length == 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Repair document has no receive effects to delete.");
        }

        var instanceIds = returnedLines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        var laterDeps = await FindLaterPhaseDependenciesAsync(nameof(RepairDocument), id, MovementActionType.ReceiveFromRepair, instanceIds, cancellationToken);
        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        foreach (var line in returnedLines)
        {
            line.IsReturned = false;
            line.TargetBinLocationId = null;
            line.NewSerialNumber = null;
            line.UpdatedAt = _clock.UtcNow;
            line.UpdatedBy = user.UserName;
        }

        var itemIds = await _db.ItemInstances.Where(x => instanceIds.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(cancellationToken);
        _db.RepairDocumentLogs.RemoveRange(_db.RepairDocumentLogs.Where(x => x.RepairDocumentId == id && x.Action == "RepairReceive"));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(RepairDocument) && x.DocumentId == id && x.TransactionType == InventoryTransactionType.RepairReceive));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(RepairDocument) && x.DocumentId == id && x.ActionType == MovementActionType.ReceiveFromRepair));
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        return Deleted("repair-receive", document.Id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> DeleteQuantityAsync(int id, string type, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var document = await _db.QuantityInventoryDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Quantity inventory document not found.");
        }

        var lineKeys = document.Lines.Select(x => new { x.ItemId, x.SnCode }).Distinct().ToArray();
        var laterDeps = await GetQuantityDependencyReasonsAsync(id, lineKeys.Select(x => (x.ItemId, x.SnCode)), cancellationToken);

        if (laterDeps.Count > 0)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(laterDeps);
        }

        _db.QuantityInventoryTransactions.RemoveRange(_db.QuantityInventoryTransactions.Where(x => x.DocumentId == id));
        _db.QuantityInventoryDocumentLines.RemoveRange(document.Lines);
        _db.QuantityInventoryDocuments.Remove(document);
        await CleanupPostSideEffectsAsync(nameof(QuantityInventoryDocument), id, document.DocumentNo, cancellationToken);

        foreach (var key in lineKeys)
        {
            await RebuildQuantityInstanceAsync(document.WarehouseId, key.ItemId, key.SnCode, cancellationToken);
        }

        return Deleted(type, document.Id, document.DocumentNo);
    }

    private async Task RebuildLocationTrackedInstancesAsync(IEnumerable<int> instanceIds, CancellationToken cancellationToken)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var instances = await _db.ItemInstances.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var locations = await _db.CurrentItemLocations.Where(x => ids.Contains(x.ItemInstanceId)).ToDictionaryAsync(x => x.ItemInstanceId, cancellationToken);
        var latestHistoryIds = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId)
            .Select(g => g.OrderByDescending(x => x.PerformedAt).ThenByDescending(x => x.Id).Select(x => x.Id).FirstOrDefault())
            .ToArrayAsync(cancellationToken);
        var latestHistories = latestHistoryIds.Length == 0
            ? new Dictionary<int, ItemMovementHistory>()
            : await _db.ItemMovementHistories
                .Where(x => latestHistoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.ItemInstanceId, cancellationToken);
        var latestRows = latestHistories.Values.ToArray();
        var latestBinIds = latestRows
            .Where(x => x.ToLocationType == LocationType.BinLocation && x.ToLocationId.HasValue)
            .Select(x => x.ToLocationId!.Value)
            .Distinct()
            .ToArray();
        var binWarehouseMap = await _db.BinLocations
            .Where(x => latestBinIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.WarehouseId, cancellationToken);
        var borrowPairs = latestRows
            .Where(x => x.ToLocationType == LocationType.Borrower)
            .Select(x => new { x.DocumentId, x.ItemInstanceId })
            .Distinct()
            .ToArray();
        var repairPairs = latestRows
            .Where(x => x.ToLocationType == LocationType.RepairVendor)
            .Select(x => new { x.DocumentId, x.ItemInstanceId })
            .Distinct()
            .ToArray();
        var borrowTexts = borrowPairs.Length == 0
            ? new Dictionary<string, string?>()
            : await _db.BorrowDocumentLines
                .Where(x => borrowPairs.Select(p => p.DocumentId).Contains(x.BorrowDocumentId) && borrowPairs.Select(p => p.ItemInstanceId).Contains(x.ItemInstanceId))
                .ToDictionaryAsync(x => $"{x.BorrowDocumentId}:{x.ItemInstanceId}", x => x.TargetExternalLocation, cancellationToken);
        var repairTexts = repairPairs.Length == 0
            ? new Dictionary<string, string?>()
            : await _db.RepairDocumentLines
                .Where(x => repairPairs.Select(p => p.DocumentId).Contains(x.RepairDocumentId) && repairPairs.Select(p => p.ItemInstanceId).Contains(x.ItemInstanceId))
                .ToDictionaryAsync(x => $"{x.RepairDocumentId}:{x.ItemInstanceId}", x => x.TargetExternalLocation, cancellationToken);

        foreach (var instanceId in ids)
        {
            if (!instances.TryGetValue(instanceId, out var instance))
            {
                continue;
            }

            if (!latestHistories.TryGetValue(instanceId, out var latest))
            {
                if (locations.TryGetValue(instanceId, out var existingLocation))
                {
                    _db.CurrentItemLocations.Remove(existingLocation);
                }

                instance.IsActive = false;
                instance.UpdatedAt = _clock.UtcNow;
                instance.UpdatedBy = "system";
                continue;
            }

            instance.Status = latest.NewStatus;
            instance.IsActive = latest.NewStatus != ItemStatus.Replacement && latest.NewStatus != ItemStatus.Disposed;
            instance.UpdatedAt = _clock.UtcNow;
            instance.UpdatedBy = "system";

            if (!locations.TryGetValue(instanceId, out var location))
            {
                location = new CurrentItemLocation
                {
                    ItemInstanceId = instanceId,
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = "system"
                };
                _db.CurrentItemLocations.Add(location);
            }

            location.LocationType = latest.ToLocationType ?? LocationType.Unknown;
            location.ReferenceDocumentType = latest.DocumentType;
            location.ReferenceDocumentId = latest.DocumentId;
            location.ReferenceDocumentNo = latest.DocumentNo;
            location.UpdatedLocationAt = latest.PerformedAt;
            location.UpdatedLocationBy = latest.PerformedBy;
            location.BinLocationId = latest.ToLocationType == LocationType.BinLocation ? latest.ToLocationId : null;
            location.ExternalPartyId = latest.ToLocationType is LocationType.Borrower or LocationType.RepairVendor ? latest.ToLocationId : null;
            location.ExternalLocationText = latest.ToLocationType switch
            {
                LocationType.Borrower => borrowTexts.GetValueOrDefault($"{latest.DocumentId}:{latest.ItemInstanceId}"),
                LocationType.RepairVendor => repairTexts.GetValueOrDefault($"{latest.DocumentId}:{latest.ItemInstanceId}"),
                _ => null
            };
            location.WarehouseId = location.BinLocationId.HasValue && binWarehouseMap.TryGetValue(location.BinLocationId.Value, out var warehouseId)
                ? warehouseId
                : null;
        }
    }

    private async Task RebuildQuantityInstanceAsync(int warehouseId, int itemId, string snCode, CancellationToken cancellationToken)
    {
        var balances = await _db.QuantityStockBalances.Where(x => x.ItemId == itemId && x.SnCode == snCode).ToListAsync(cancellationToken);
        _db.QuantityStockBalances.RemoveRange(balances);

        var txs = await _db.QuantityInventoryTransactions
            .Where(x => x.ItemId == itemId && x.SnCode == snCode)
            .OrderBy(x => x.PostedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var grouped = txs
            .GroupBy(x => new { x.WarehouseId, x.ItemId, x.SnCode, x.StatusAfter })
            .Select(g => new QuantityStockBalance
            {
                WarehouseId = g.Key.WarehouseId,
                ItemId = g.Key.ItemId,
                SnCode = g.Key.SnCode,
                Status = g.Key.StatusAfter,
                Quantity = g.Sum(x => x.QuantityDelta),
                CreatedAt = _clock.UtcNow,
                CreatedBy = "system"
            })
            .Where(x => x.Quantity != 0)
            .ToArray();

        _db.QuantityStockBalances.AddRange(grouped);

        var instance = await _db.ItemInstances.FirstOrDefaultAsync(x =>
            x.ItemId == itemId &&
            x.SerialNumber == snCode &&
            x.TrackingType == ItemTrackingType.QuantityOnly, cancellationToken);

        if (instance == null)
        {
            return;
        }

        var lastTx = txs.LastOrDefault();
        if (lastTx == null)
        {
            instance.IsActive = false;
            instance.UpdatedAt = _clock.UtcNow;
            instance.UpdatedBy = "system";
            return;
        }

        instance.Status = lastTx.StatusAfter;
        instance.IsActive = grouped.Sum(x => x.Quantity) > 0;
        instance.UpdatedAt = _clock.UtcNow;
        instance.UpdatedBy = "system";
    }

    private async Task RecalculateLocationTrackedStockAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var existing = await _db.StockBalances.Where(x => ids.Contains(x.ItemId)).ToListAsync(cancellationToken);
        _db.StockBalances.RemoveRange(existing);

        var rebuilt = await _db.CurrentItemLocations
            .AsNoTracking()
            .Include(x => x.ItemInstance)
            .Where(x =>
                x.BinLocationId.HasValue &&
                x.WarehouseId.HasValue &&
                x.ItemInstance != null &&
                ids.Contains(x.ItemInstance.ItemId) &&
                x.ItemInstance.IsActive &&
                x.ItemInstance.TrackingType == ItemTrackingType.LocationTracked)
            .GroupBy(x => new
            {
                x.WarehouseId,
                x.BinLocationId,
                ItemId = x.ItemInstance!.ItemId,
                Status = x.ItemInstance.Status
            })
            .Select(g => new StockBalance
            {
                WarehouseId = g.Key.WarehouseId!.Value,
                BinLocationId = g.Key.BinLocationId,
                ItemId = g.Key.ItemId,
                Status = g.Key.Status,
                Quantity = g.Count(),
                CreatedAt = _clock.UtcNow,
                CreatedBy = "system"
            })
            .ToListAsync(cancellationToken);

        _db.StockBalances.AddRange(rebuilt);
    }

    private async Task<List<string>> FindLaterHistoryDependenciesAsync(string documentType, int documentId, IEnumerable<int> instanceIds, CancellationToken cancellationToken)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new List<string>();
        }

        var maxPerInstance = await _db.ItemMovementHistories
            .Where(x => x.DocumentType == documentType && x.DocumentId == documentId && ids.Contains(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId)
            .Select(g => new { ItemInstanceId = g.Key, LastAt = g.Max(x => x.PerformedAt), LastId = g.Max(x => x.Id) })
            .ToListAsync(cancellationToken);

        var laterCandidates = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId) && !(x.DocumentType == documentType && x.DocumentId == documentId))
            .Select(x => new { x.ItemInstanceId, x.PerformedAt, x.Id })
            .ToListAsync(cancellationToken);

        return maxPerInstance
            .Where(item => laterCandidates.Any(x =>
                x.ItemInstanceId == item.ItemInstanceId &&
                (x.PerformedAt > item.LastAt || (x.PerformedAt == item.LastAt && x.Id > item.LastId))))
            .Select(item => $"Item instance {item.ItemInstanceId} has downstream operations.")
            .ToList();
    }

    private async Task<List<string>> FindLaterPhaseDependenciesAsync(string documentType, int documentId, MovementActionType actionType, IEnumerable<int> instanceIds, CancellationToken cancellationToken)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new List<string>();
        }

        var phaseMarks = await _db.ItemMovementHistories
            .Where(x => x.DocumentType == documentType && x.DocumentId == documentId && x.ActionType == actionType && ids.Contains(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId)
            .Select(g => new { ItemInstanceId = g.Key, LastAt = g.Max(x => x.PerformedAt), LastId = g.Max(x => x.Id) })
            .ToListAsync(cancellationToken);

        var laterCandidates = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId) && !(x.DocumentType == documentType && x.DocumentId == documentId && x.ActionType == actionType))
            .Select(x => new { x.ItemInstanceId, x.PerformedAt, x.Id })
            .ToListAsync(cancellationToken);

        return phaseMarks
            .Where(item => laterCandidates.Any(x =>
                x.ItemInstanceId == item.ItemInstanceId &&
                (x.PerformedAt > item.LastAt || (x.PerformedAt == item.LastAt && x.Id > item.LastId))))
            .Select(item => $"Item instance {item.ItemInstanceId} has downstream operations after {actionType}.")
            .ToList();
    }

    private async Task<List<string>> GetQuantityDependencyReasonsAsync(int documentId, IEnumerable<(int ItemId, string SnCode)> keys, CancellationToken cancellationToken)
    {
        var normalizedKeys = keys
            .Where(x => !string.IsNullOrWhiteSpace(x.SnCode))
            .Select(x => (x.ItemId, SnCode: x.SnCode.Trim()))
            .Distinct()
            .ToArray();
        if (normalizedKeys.Length == 0)
        {
            return new List<string>();
        }

        var itemIds = normalizedKeys.Select(x => x.ItemId).Distinct().ToArray();
        var snCodes = normalizedKeys.Select(x => x.SnCode).Distinct().ToArray();
        var keySet = normalizedKeys.Select(x => QuantityKey(x.ItemId, x.SnCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var latestByKey = await _db.QuantityInventoryTransactions
            .Where(x => x.DocumentId == documentId && itemIds.Contains(x.ItemId) && snCodes.Contains(x.SnCode))
            .GroupBy(x => new { x.ItemId, x.SnCode })
            .Select(g => g.OrderByDescending(x => x.PostedAt).ThenByDescending(x => x.Id)
                .Select(x => new { x.ItemId, x.SnCode, x.PostedAt, x.Id })
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        var effectiveMarks = latestByKey
            .Where(x => x != null && keySet.Contains(QuantityKey(x!.ItemId, x.SnCode)))
            .Select(x => x!)
            .ToArray();
        if (effectiveMarks.Length == 0)
        {
            return new List<string>();
        }

        var laterCandidates = await _db.QuantityInventoryTransactions
            .Where(x => x.DocumentId != documentId && itemIds.Contains(x.ItemId) && snCodes.Contains(x.SnCode))
            .Select(x => new { x.ItemId, x.SnCode, x.PostedAt, x.Id })
            .ToListAsync(cancellationToken);

        return effectiveMarks
            .Where(mark => laterCandidates.Any(x =>
                x.ItemId == mark.ItemId &&
                string.Equals(x.SnCode, mark.SnCode, StringComparison.OrdinalIgnoreCase) &&
                (x.PostedAt > mark.PostedAt || (x.PostedAt == mark.PostedAt && x.Id > mark.Id))))
            .Select(mark => $"Quantity item {mark.ItemId}/{mark.SnCode} has later quantity transactions.")
            .Distinct()
            .ToList();
    }

    private async Task CleanupPostSideEffectsAsync(string entityName, int entityId, string documentNo, CancellationToken cancellationToken)
    {
        var notifications = await _db.Notifications.Where(x => x.LinkUrl != null && x.LinkUrl.Contains(Uri.EscapeDataString(documentNo))).ToListAsync(cancellationToken);
        _db.Notifications.RemoveRange(notifications);
    }

    private void AddAuditLog(CurrentUserContext user, string action, string entityName, int entityId, string documentNo, string reason, string? afterJson = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNo = documentNo,
            AfterJson = afterJson,
            Result = reason,
            CreatedAt = _clock.UtcNow
        });
    }

    private static string EntityNameForType(string type) => type switch
    {
        "inbound" => nameof(InboundDocument),
        "move" => nameof(MoveDocument),
        "adjustment" => nameof(AdjustmentDocument),
        "borrow-lend" or "borrow-return" => nameof(BorrowDocument),
        "repair-send" or "repair-receive" => nameof(RepairDocument),
        "quantity-receive" or "quantity-issue" or "quantity-adjust" => nameof(QuantityInventoryDocument),
        _ => type
    };

    private async Task<IReadOnlyCollection<DocumentAuditEventDto>> BuildAuditTrailAsync(string entityName, int entityId, string documentNo, CancellationToken cancellationToken)
    {
        return await _db.AuditLogs.AsNoTracking()
            .Where(x => (x.EntityName == entityName && x.EntityId == entityId) || (x.ReferenceNo == documentNo && x.EntityName == entityName))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DocumentAuditEventDto
            {
                Action = x.Action,
                Operator = x.UserName,
                Reason = x.Result,
                Timestamp = x.CreatedAt
            })
            .ToArrayAsync(cancellationToken);
    }

    private async Task<List<string>> GetDependencyReasonsAsync(string type, int id, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "inbound":
            {
                var document = await _db.InboundDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                var instanceIds = document.Lines.Where(x => x.ItemInstanceId.HasValue).Select(x => x.ItemInstanceId!.Value).ToArray();
                return await FindLaterHistoryDependenciesAsync(nameof(InboundDocument), id, instanceIds, cancellationToken);
            }
            case "move":
            {
                var document = await _db.MoveDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                return await FindLaterHistoryDependenciesAsync(nameof(MoveDocument), id, document.Lines.Select(x => x.ItemInstanceId), cancellationToken);
            }
            case "adjustment":
            {
                var document = await _db.AdjustmentDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                return await FindLaterHistoryDependenciesAsync(nameof(AdjustmentDocument), id, document.Lines.Select(x => x.ItemInstanceId), cancellationToken);
            }
            case "borrow-lend":
            {
                var document = await _db.BorrowDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                if (document.Lines.Any(x => x.IsReturned)) return new List<string> { "Cannot edit or delete borrow lend after return has been posted. Delete borrow return first." };
                return await FindLaterHistoryDependenciesAsync(nameof(BorrowDocument), id, document.Lines.Select(x => x.ItemInstanceId), cancellationToken);
            }
            case "borrow-return":
            {
                var document = await _db.BorrowDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                var returned = document.Lines.Where(x => x.IsReturned).Select(x => x.ItemInstanceId).ToArray();
                return await FindLaterPhaseDependenciesAsync(nameof(BorrowDocument), id, MovementActionType.ReturnBorrowed, returned, cancellationToken);
            }
            case "repair-send":
            {
                var document = await _db.RepairDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                if (document.Lines.Any(x => x.IsReturned)) return new List<string> { "Cannot edit or delete repair send after repair receive has been posted. Delete repair receive first." };
                return await FindLaterHistoryDependenciesAsync(nameof(RepairDocument), id, document.Lines.Select(x => x.ItemInstanceId), cancellationToken);
            }
            case "repair-receive":
            {
                var document = await _db.RepairDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                var returned = document.Lines.Where(x => x.IsReturned).Select(x => x.ItemInstanceId).ToArray();
                return await FindLaterPhaseDependenciesAsync(nameof(RepairDocument), id, MovementActionType.ReceiveFromRepair, returned, cancellationToken);
            }
            case "quantity-receive":
            case "quantity-issue":
            case "quantity-adjust":
            {
                var document = await _db.QuantityInventoryDocuments.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return new List<string> { "Document not found." };
                return await GetQuantityDependencyReasonsAsync(id, document.Lines.Select(x => (x.ItemId, x.SnCode)), cancellationToken);
            }
            default:
                return new List<string>();
        }
    }

    private async Task<object?> BuildInboundPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.InboundDocuments
            .AsNoTracking()
            .Include(x => x.Receiver)
            .Include(x => x.SourceExternalParty)
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)
            .Include(x => x.Lines).ThenInclude(x => x.BinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        return new
        {
            sourcePartyCode = document.SourceExternalParty?.PartyCode ?? string.Empty,
            sourcePartyName = document.SourceExternalParty?.Name ?? string.Empty,
            receiverCode = document.Receiver?.PartyCode ?? string.Empty,
            receiverName = document.Receiver?.Name ?? string.Empty,
            receiverPhone = document.PartyPhone,
            receiverDepartment = document.PartyDepartment,
            departmentOwner = document.DepartmentOwner,
            approvedBy = document.ApprovedBy,
            documentNo = document.DocumentNo,
            warehouseId = document.WarehouseId,
            documentDate = document.DocumentDate,
            note = document.Note,
            ownerName = document.Lines.Select(x => x.ItemInstance!.OwnerName).FirstOrDefault(x => x != null),
            lines = document.Lines.Select(x => new
            {
                itemCode = x.Item?.ItemCode ?? string.Empty,
                serialNumber = x.SerialNumber,
                mt = x.ItemInstance?.MT,
                quantity = x.Quantity,
                binCode = x.BinLocation?.BinCode ?? string.Empty,
                condition = x.Condition,
                note = x.Note
            }).ToArray()
        };
    }

    private async Task<object?> BuildMovePayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.MoveDocuments
            .AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        return new
        {
            documentNo = document.DocumentNo,
            warehouseId = document.WarehouseId,
            documentDate = document.DocumentDate,
            note = document.Note,
            lines = document.Lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                targetBinCode = x.TargetBinLocation?.BinCode ?? string.Empty,
                note = x.Note
            }).ToArray()
        };
    }

    private async Task<object?> BuildAdjustmentPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.AdjustmentDocuments
            .AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        var targetBinIds = document.Lines.Where(x => x.TargetBinLocationId.HasValue).Select(x => x.TargetBinLocationId!.Value).Distinct().ToArray();
        var binMap = targetBinIds.Length == 0
            ? new Dictionary<int, string>()
            : await _db.BinLocations.AsNoTracking().Where(x => targetBinIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.BinCode, cancellationToken);
        return new
        {
            documentNo = document.DocumentNo,
            warehouseId = document.WarehouseId,
            documentDate = document.DocumentDate,
            reason = document.Reason,
            lines = document.Lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                newSerialNumber = string.Empty,
                newStatus = x.NewStatus,
                targetBinCode = x.TargetBinLocationId.HasValue && binMap.TryGetValue(x.TargetBinLocationId.Value, out var binCode) ? binCode : null,
                reason = x.Reason
            }).ToArray()
        };
    }

    private async Task<object?> BuildBorrowLendPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.BorrowDocuments
            .AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        return new
        {
            documentNo = document.DocumentNo,
            warehouseId = await ResolveBorrowWarehouseIdAsync(document.Lines.Select(x => x.FromBinLocationId), cancellationToken),
            borrowerCode = document.Borrower?.PartyCode ?? string.Empty,
            borrowerName = document.Borrower?.Name ?? string.Empty,
            borrowDate = document.DocumentDate,
            dueDate = document.DueDate,
            purpose = document.Purpose,
            borrowDepartment = document.BorrowDepartment,
            approvedBy = document.ApprovedBy,
            borrowerPhone = document.BorrowerPhone,
            departmentOwner = document.DepartmentOwner,
            lines = document.Lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                targetExternalLocation = x.TargetExternalLocation,
                note = x.Note
            }).ToArray()
        };
    }

    private async Task<int?> ResolveBorrowWarehouseIdAsync(IEnumerable<int?> fromBinIds, CancellationToken cancellationToken)
    {
        var binId = fromBinIds.FirstOrDefault(x => x.HasValue);
        if (!binId.HasValue) return null;
        return await _db.BinLocations.Where(x => x.Id == binId.Value).Select(x => (int?)x.WarehouseId).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<object?> BuildBorrowReturnPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.BorrowDocuments
            .AsNoTracking()
            .Include(x => x.Borrower)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        var lines = document.Lines.Where(x => x.IsReturned).ToArray();
        return new
        {
            borrowDocumentId = document.Id,
            borrowDocumentNo = document.DocumentNo,
            returnDate = lines.Select(x => x.ReturnedAt).FirstOrDefault(x => x.HasValue) ?? document.UpdatedAt ?? document.DocumentDate,
            returnerCode = document.Borrower?.PartyCode ?? string.Empty,
            returnerName = document.Borrower?.Name ?? string.Empty,
            borrowDepartment = document.BorrowDepartment,
            approvedBy = document.ApprovedBy,
            borrowerPhone = document.BorrowerPhone,
            departmentOwner = document.DepartmentOwner,
            lines = lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                condition = x.ReturnCondition,
                targetBinCode = x.TargetBinLocation?.BinCode,
                note = x.Note
            }).ToArray()
        };
    }

    private async Task<object?> BuildRepairSendPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.RepairDocuments
            .AsNoTracking()
            .Include(x => x.RepairVendor)
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        return new
        {
            documentNo = document.DocumentNo,
            repairSenderCode = document.RepairVendor?.PartyCode ?? string.Empty,
            repairSenderName = document.RepairVendor?.Name ?? string.Empty,
            sendDate = document.DocumentDate,
            expectedReturnDate = document.ExpectedReturnDate,
            reason = document.Reason,
            lines = document.Lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                targetExternalLocation = x.TargetExternalLocation,
                note = x.RepairResultNote
            }).ToArray()
        };
    }

    private async Task<object?> BuildRepairReceivePayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.RepairDocuments
            .AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .Include(x => x.Lines).ThenInclude(x => x.TargetBinLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        var lines = document.Lines.Where(x => x.IsReturned).ToArray();
        return new
        {
            repairDocumentId = document.Id,
            repairDocumentNo = document.DocumentNo,
            resultNote = lines.Select(x => x.RepairResultNote).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            lines = lines.Select(x => new
            {
                itemCode = x.ItemInstance?.Item?.ItemCode ?? string.Empty,
                serialNumber = x.ItemInstance?.SerialNumber ?? string.Empty,
                targetBinCode = x.TargetBinLocation?.BinCode ?? string.Empty,
                newSerialNumber = x.NewSerialNumber,
                note = x.RepairResultNote,
                result = RepairResult.Success
            }).ToArray()
        };
    }

    private async Task<object?> BuildQuantityPayloadAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _db.QuantityInventoryDocuments
            .AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document == null) return null;
        var itemId = document.Lines.Select(x => x.ItemId).FirstOrDefault();
        var instance = await _db.ItemInstances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId && x.TrackingType == ItemTrackingType.QuantityOnly, cancellationToken);
        var item = document.Lines.Select(x => x.Item).FirstOrDefault(x => x != null);
        var categoryCode = item == null
            ? string.Empty
            : await _db.ItemCategories.Where(x => x.Id == item.CategoryId).Select(x => x.CategoryCode).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return new
        {
            documentNo = document.DocumentNo,
            warehouseId = document.WarehouseId,
            documentDate = document.DocumentDate,
            itemCategoryCode = categoryCode,
            itemCode = item?.ItemCode ?? string.Empty,
            approvedBy = document.ApprovedBy,
            note = document.Note,
            ownerName = instance?.OwnerName,
            lines = document.Lines.Select(x => new
            {
                snCode = x.SnCode,
                status = x.Status,
                quantity = x.Quantity,
                note = x.Note
            }).ToArray()
        };
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

    private async Task<LifecycleTransactionScope> BeginLifecycleTransactionAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return new LifecycleTransactionScope(null, ownsTransaction: false);
        }

        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new LifecycleTransactionScope(transaction, ownsTransaction: true);
    }

    private static string QuantityKey(int itemId, string? snCode)
        => $"{itemId}:{snCode?.Trim().ToUpperInvariant()}";

    private static string? NormalizeType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "inbound" => normalized,
            "move" => normalized,
            "adjustment" => normalized,
            "borrow-lend" => normalized,
            "borrow-return" => normalized,
            "repair-send" => normalized,
            "repair-receive" => normalized,
            "quantity-receive" => normalized,
            "quantity-issue" => normalized,
            "quantity-adjust" => normalized,
            _ => null
        };
    }

    private async Task<string?> GetDocumentNoAsync(string type, int id, CancellationToken cancellationToken)
    {
        return type switch
        {
            "inbound" => await _db.InboundDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            "move" => await _db.MoveDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            "adjustment" => await _db.AdjustmentDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            "borrow-lend" or "borrow-return" => await _db.BorrowDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            "repair-send" or "repair-receive" => await _db.RepairDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await _db.QuantityInventoryDocuments.Where(x => x.Id == id).Select(x => x.DocumentNo).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private static JsonElement ForceDocumentNo(JsonElement payload, string documentNo)
    {
        var node = JsonNode.Parse(payload.GetRawText())?.AsObject() ?? new JsonObject();
        node["documentNo"] = documentNo;
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private ServiceResult<DocumentMutationResultDto> Deleted(string type, int id, string documentNo)
    {
        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Delete",
            DocumentId = id,
            DocumentNo = documentNo,
            DocumentType = type,
            ProcessedAt = _clock.UtcNow
        }, "Document deleted.");
    }

    private sealed class LifecycleTransactionScope : IAsyncDisposable
    {
        private readonly IDbContextTransaction? _transaction;

        public LifecycleTransactionScope(IDbContextTransaction? transaction, bool ownsTransaction)
        {
            _transaction = transaction;
            OwnsTransaction = ownsTransaction;
        }

        public bool OwnsTransaction { get; }

        public Task CommitAsync(CancellationToken cancellationToken)
            => OwnsTransaction && _transaction != null
                ? _transaction.CommitAsync(cancellationToken)
                : Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken)
            => OwnsTransaction && _transaction != null
                ? _transaction.RollbackAsync(cancellationToken)
                : Task.CompletedTask;

        public ValueTask DisposeAsync()
            => OwnsTransaction && _transaction != null
                ? _transaction.DisposeAsync()
                : ValueTask.CompletedTask;
    }
}
