using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using ERP.Inventory.Domain.Common;
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
    private readonly IDocumentRollbackService _rollbackService;

    public DocumentLifecycleService(
        InventoryDbContext db,
        IDateTimeProvider clock,
        IInboundService inboundService,
        IInventoryOperationService moveService,
        IRepairService repairService,
        IBorrowService borrowService,
        IQuantityInventoryService quantityService,
        AdjustmentService adjustmentService,
        IDocumentRollbackService rollbackService)
    {
        _db = db;
        _clock = clock;
        _inboundService = inboundService;
        _moveService = moveService;
        _repairService = repairService;
        _borrowService = borrowService;
        _quantityService = quantityService;
        _adjustmentService = adjustmentService;
        _rollbackService = rollbackService;
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
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return ServiceResult<DocumentMutationResultDto>.Fail(ex.Message);
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
            var result = await RebuildInternalAsync(normalizedType, id, document.RootElement.Clone(), user, cancellationToken, editModel.Data.DocumentNo);
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
            "quantity-receive" or "quantity-issue" or "quantity-adjust" => await EditQuantityAsync(id, type, payload, user, cancellationToken),
            "inbound" => await EditInboundSelectiveAsync(id, payload, user, cancellationToken),
            "move" => await EditMoveSelectiveAsync(id, payload, user, cancellationToken),
            "borrow-lend" => await EditBorrowLendSelectiveAsync(id, payload, user, cancellationToken),
            "borrow-return" => await EditBorrowReturnSelectiveAsync(id, payload, user, cancellationToken),
            "repair-send" => await EditRepairSendSelectiveAsync(id, payload, user, cancellationToken),
            "repair-receive" => await EditRepairReceiveSelectiveAsync(id, payload, user, cancellationToken),
            "adjustment" => await EditAdjustmentSelectiveAsync(id, payload, user, cancellationToken),
            _ => ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.")
        };
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> RebuildInternalAsync(string type, int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken, string? preservedDocumentNo = null)
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

    private async Task<ServiceResult<DocumentMutationResultDto>> EditQuantityAsync(int id, string type, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        // RC-1 FIX: Must delete existing document FIRST before reposting.
        // The old code called RepostAsync directly, which created a second set of
        // QuantityInventoryTransactions on top of the existing ones → duplicate rows
        // and wrong stock balances. Pattern mirrors RebuildStandaloneAsync.
        var documentNo = await GetDocumentNoAsync(type, id, cancellationToken);
        if (documentNo == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Document not found.");
        }

        var deleteResult = await DeleteQuantityAsync(id, type, user, cancellationToken);
        if (!deleteResult.Success)
        {
            return deleteResult;
        }

        // Flush staged deletes before repost: QuantityInventory services use
        // AsNoTracking queries that bypass the EF change tracker and read from DB.
        // Without this flush the deleted document is still visible and the repost
        // would try to append to it rather than create fresh.
        await _db.SaveChangesAsync(cancellationToken);

        var patchedPayload = ForceDocumentNo(payload, documentNo);
        var postResult = await RepostAsync(type, patchedPayload, user, cancellationToken);
        if (!postResult.Success || postResult.Data == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(postResult.Errors);
        }

        AddAuditLog(user, "Edit", EntityNameForType(type), id, documentNo,
            "Quantity document edited: old document deleted and effects rebuilt from new payload.",
            payload.GetRawText());

        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Edit",
            DocumentId = postResult.Data.DocumentId,
            DocumentNo = postResult.Data.DocumentNo,
            DocumentType = type,
            ProcessedAt = _clock.UtcNow
        }, "Document updated.");
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditHeaderOnlyAsync(int id, string type, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var currentPayload = await GetEditModelAsync(type, id, user, cancellationToken);
        if (!currentPayload.Success || currentPayload.Data == null)
        {
            return ServiceResult<DocumentMutationResultDto>.Fail(currentPayload.Errors.Count > 0 ? currentPayload.Errors : new[] { "Document not found." });
        }

        if (!LinesEquivalent(currentPayload.Data.Payload, payload))
        {
            return ServiceResult<DocumentMutationResultDto>.Fail("Normal edit no longer performs full delete/repost rebuild. Line-level changes for this document type require selective mutation support; use explicit Rebuild only for recovery/full replay.");
        }

        var result = await UpdateDocumentHeaderAsync(id, type, payload, user, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        AddAuditLog(user, "Edit", EntityNameForType(type), result.Data!.DocumentId, result.Data.DocumentNo, "Document header edited without rebuilding effects.", payload.GetRawText());
        return result;
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> UpdateDocumentHeaderAsync(int id, string type, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        switch (type)
        {
            case "inbound":
            {
                var request = JsonSerializer.Deserialize<InboundRequest>(payload.GetRawText(), JsonOptions);
                var document = await _db.InboundDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Inbound document not found.");
                if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");
                document.DocumentDate = request.DocumentDate;
                document.PartyDepartment = request.ReceiverDepartment.Trim();
                document.PartyPhone = request.ReceiverPhone.Trim();
                document.DepartmentOwner = request.DepartmentOwner.Trim();
                document.ApprovedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) ? user.UserName : request.ApprovedBy.Trim();
                document.ApprovedAt = request.DocumentDate;
                document.PostedAt = request.DocumentDate;
                document.Note = request.Note;
                Touch(document, user, now);
                return Edited(type, document.Id, document.DocumentNo);
            }
            case "move":
            {
                var request = JsonSerializer.Deserialize<MoveLocationRequest>(payload.GetRawText(), JsonOptions);
                var document = await _db.MoveDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Move document not found.");
                if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");
                document.DocumentDate = request.DocumentDate;
                document.Note = request.Note;
                Touch(document, user, now);
                return Edited(type, document.Id, document.DocumentNo);
            }
            case "adjustment":
            {
                var request = JsonSerializer.Deserialize<AdjustmentRequest>(payload.GetRawText(), JsonOptions);
                var document = await _db.AdjustmentDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Adjustment document not found.");
                if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");
                document.DocumentDate = request.DocumentDate;
                document.Reason = request.Reason;
                Touch(document, user, now);
                return Edited(type, document.Id, document.DocumentNo);
            }
            case "borrow-lend":
            case "borrow-return":
            {
                var document = await _db.BorrowDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");
                if (type == "borrow-lend")
                {
                    var request = JsonSerializer.Deserialize<BorrowLendRequest>(payload.GetRawText(), JsonOptions);
                    if (request == null) return ServiceResult<DocumentMutationResultDto>.Fail("Invalid borrow lend payload.");
                    document.DocumentDate = request.BorrowDate;
                    document.DueDate = request.DueDate;
                    document.Purpose = request.Purpose;
                    document.BorrowDepartment = request.BorrowDepartment;
                    document.BorrowerPhone = request.BorrowerPhone;
                    document.DepartmentOwner = request.DepartmentOwner;
                    document.ApprovedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) ? user.UserName : request.ApprovedBy.Trim();
                }
                else
                {
                    var request = JsonSerializer.Deserialize<BorrowReturnRequest>(payload.GetRawText(), JsonOptions);
                    if (request == null) return ServiceResult<DocumentMutationResultDto>.Fail("Invalid borrow return payload.");
                    document.BorrowDepartment = request.BorrowDepartment;
                    document.BorrowerPhone = request.BorrowerPhone;
                    document.DepartmentOwner = request.DepartmentOwner;
                    document.ApprovedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) ? document.ApprovedBy : request.ApprovedBy.Trim();
                }

                Touch(document, user, now);
                return Edited(type, document.Id, document.DocumentNo);
            }
            case "repair-send":
            case "repair-receive":
            {
                var document = await _db.RepairDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");
                if (type == "repair-send")
                {
                    var request = JsonSerializer.Deserialize<RepairSendRequest>(payload.GetRawText(), JsonOptions);
                    if (request == null) return ServiceResult<DocumentMutationResultDto>.Fail("Invalid repair send payload.");
                    document.DocumentDate = request.SendDate;
                    document.ExpectedReturnDate = request.ExpectedReturnDate;
                    document.Reason = request.Reason;
                }
                else
                {
                    var request = JsonSerializer.Deserialize<RepairReceiveRequest>(payload.GetRawText(), JsonOptions);
                    if (request == null) return ServiceResult<DocumentMutationResultDto>.Fail("Invalid repair receive payload.");
                    document.Note = request.Note ?? request.ResultNote;
                }

                Touch(document, user, now);
                return Edited(type, document.Id, document.DocumentNo);
            }
            default:
                return ServiceResult<DocumentMutationResultDto>.Fail($"Unsupported document type '{type}'.");
        }
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditMoveSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<MoveLocationRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.MoveDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Move document not found.");
        if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");

        var prepared = await PrepareMoveLinesAsync(request, user, cancellationToken);
        if (!prepared.Success) return ServiceResult<DocumentMutationResultDto>.Fail(prepared.Errors);

        var incomingByInstance = prepared.Data!.ToDictionary(x => x.Instance.Id);
        var existingByInstance = document.Lines.ToDictionary(x => x.ItemInstanceId);
        var affectedIds = existingByInstance
            .Where(x => !incomingByInstance.ContainsKey(x.Key) || incomingByInstance[x.Key].TargetBin.Id != x.Value.TargetBinLocationId || !Same(incomingByInstance[x.Key].Line.Note, x.Value.Note))
            .Select(x => x.Key)
            .Concat(incomingByInstance.Keys.Where(x => !existingByInstance.ContainsKey(x)))
            .Distinct()
            .ToArray();

        var deps = await FindLaterHistoryDependenciesAsync(nameof(MoveDocument), id, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        document.DocumentDate = request.DocumentDate;
        document.Note = request.Note;
        Touch(document, user, _clock.UtcNow);

        await ReverseLocationTrackedLinesAsync(nameof(MoveDocument), id, affectedIds, document.DocumentNo, logCleanup: null, cancellationToken);
        var affectedPrepared = prepared.Data!.Where(x => affectedIds.Contains(x.Instance.Id)).ToArray();
        var addResult = await AddMoveEffectsAsync(document, affectedPrepared, user, cancellationToken);
        if (!addResult.Success) return addResult;

        AddAuditLog(user, "Edit", nameof(MoveDocument), id, document.DocumentNo, "Move document selectively edited.", payload.GetRawText());
        return Edited("move", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditBorrowLendSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<BorrowLendRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.BorrowDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");
        if (document.Lines.Any(x => x.IsReturned)) return ServiceResult<DocumentMutationResultDto>.Fail("Cannot edit borrow lend after return has been posted. Delete borrow return first.");

        var incoming = await ResolveLineInstancesAsync(request.Lines.Select(x => (x.ItemCode, x.SerialNumber)), cancellationToken);
        if (!incoming.Success) return ServiceResult<DocumentMutationResultDto>.Fail(incoming.Errors);
        var incomingByInstance = incoming.Data!.Zip(request.Lines, (i, l) => new { Instance = i, Line = l }).ToDictionary(x => x.Instance.Id);
        var existingByInstance = document.Lines.ToDictionary(x => x.ItemInstanceId);
        var affectedIds = existingByInstance
            .Where(x => !incomingByInstance.ContainsKey(x.Key) || !Same(incomingByInstance[x.Key].Line.TargetExternalLocation, x.Value.TargetExternalLocation) || !Same(incomingByInstance[x.Key].Line.Note, x.Value.Note))
            .Select(x => x.Key)
            .Concat(incomingByInstance.Keys.Where(x => !existingByInstance.ContainsKey(x)))
            .Distinct()
            .ToArray();

        var deps = await FindLaterHistoryDependenciesAsync(nameof(BorrowDocument), id, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await UpdateDocumentHeaderAsync(id, "borrow-lend", payload, user, cancellationToken);
        await ReverseLocationTrackedLinesAsync(nameof(BorrowDocument), id, affectedIds, document.DocumentNo, ids => _db.BorrowDocumentLogs.Where(x => x.BorrowDocumentId == id && x.Action == "BorrowIssue" && ids.Contains(x.ItemInstanceId)), cancellationToken);
        var affectedSet = affectedIds.ToHashSet();
        var linesToPost = incomingByInstance.Values.Where(x => affectedSet.Contains(x.Instance.Id)).Select(x => x.Line).ToArray();
        if (linesToPost.Length > 0)
        {
            var postRequest = new BorrowLendRequest { DocumentNo = document.DocumentNo, WarehouseId = request.WarehouseId, WarehouseCode = request.WarehouseCode, Borrower = request.Borrower, BorrowerCode = request.BorrowerCode, BorrowerName = request.BorrowerName, BorrowDate = request.BorrowDate, DueDate = request.DueDate, Purpose = request.Purpose, BorrowDepartment = request.BorrowDepartment, ApprovedBy = request.ApprovedBy, BorrowerPhone = request.BorrowerPhone, DepartmentOwner = request.DepartmentOwner, Lines = linesToPost };
            var post = await _borrowService.LendAsync(postRequest, user, cancellationToken);
            if (!post.Success) return ServiceResult<DocumentMutationResultDto>.Fail(post.Errors);
        }

        AddAuditLog(user, "Edit", nameof(BorrowDocument), id, document.DocumentNo, "Borrow lend selectively edited.", payload.GetRawText());
        return Edited("borrow-lend", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditRepairSendSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<RepairSendRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.RepairDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");
        if (document.Lines.Any(x => x.IsReturned)) return ServiceResult<DocumentMutationResultDto>.Fail("Cannot edit repair send after repair receive has been posted. Delete repair receive first.");

        var incoming = await ResolveLineInstancesAsync(request.Lines.Select(x => (x.ItemCode, x.SerialNumber)), cancellationToken);
        if (!incoming.Success) return ServiceResult<DocumentMutationResultDto>.Fail(incoming.Errors);
        var incomingByInstance = incoming.Data!.Zip(request.Lines, (i, l) => new { Instance = i, Line = l }).ToDictionary(x => x.Instance.Id);
        var existingByInstance = document.Lines.ToDictionary(x => x.ItemInstanceId);
        var affectedIds = existingByInstance
            .Where(x => !incomingByInstance.ContainsKey(x.Key) || !Same(incomingByInstance[x.Key].Line.TargetExternalLocation, x.Value.TargetExternalLocation) || !Same(incomingByInstance[x.Key].Line.Note, x.Value.RepairResultNote))
            .Select(x => x.Key)
            .Concat(incomingByInstance.Keys.Where(x => !existingByInstance.ContainsKey(x)))
            .Distinct()
            .ToArray();

        var deps = await FindLaterHistoryDependenciesAsync(nameof(RepairDocument), id, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await UpdateDocumentHeaderAsync(id, "repair-send", payload, user, cancellationToken);
        await ReverseLocationTrackedLinesAsync(nameof(RepairDocument), id, affectedIds, document.DocumentNo, ids => _db.RepairDocumentLogs.Where(x => x.RepairDocumentId == id && x.Action == "RepairSend" && ids.Contains(x.ItemInstanceId)), cancellationToken);
        var affectedSet = affectedIds.ToHashSet();
        var linesToPost = incomingByInstance.Values.Where(x => affectedSet.Contains(x.Instance.Id)).Select(x => x.Line).ToArray();
        if (linesToPost.Length > 0)
        {
            var postRequest = new RepairSendRequest { DocumentNo = document.DocumentNo, RepairSenderCode = request.RepairSenderCode, RepairSenderName = request.RepairSenderName, SendDate = request.SendDate, ExpectedReturnDate = request.ExpectedReturnDate, Reason = request.Reason, Lines = linesToPost };
            var post = await _repairService.SendToRepairAsync(postRequest, user, cancellationToken);
            if (!post.Success) return ServiceResult<DocumentMutationResultDto>.Fail(post.Errors);
        }

        AddAuditLog(user, "Edit", nameof(RepairDocument), id, document.DocumentNo, "Repair send selectively edited.", payload.GetRawText());
        return Edited("repair-send", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditBorrowReturnSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<BorrowReturnRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.BorrowDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Borrow document not found.");

        var incoming = await ResolveLineInstancesAsync(request.Lines.Select(x => (x.ItemCode, x.SerialNumber)), cancellationToken);
        if (!incoming.Success) return ServiceResult<DocumentMutationResultDto>.Fail(incoming.Errors);
        var incomingByInstance = incoming.Data!.Zip(request.Lines, (i, l) => new { Instance = i, Line = l }).ToDictionary(x => x.Instance.Id);
        var returnedByInstance = document.Lines.Where(x => x.IsReturned).ToDictionary(x => x.ItemInstanceId);
        var affectedIds = returnedByInstance
            .Where(x => !incomingByInstance.ContainsKey(x.Key) || incomingByInstance[x.Key].Line.Condition != x.Value.ReturnCondition || !Same(incomingByInstance[x.Key].Line.Note, x.Value.Note))
            .Select(x => x.Key)
            .Concat(incomingByInstance.Keys.Where(x => !returnedByInstance.ContainsKey(x)))
            .Distinct()
            .ToArray();

        var deps = await FindLaterPhaseDependenciesAsync(nameof(BorrowDocument), id, MovementActionType.ReturnBorrowed, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await ReverseReturnPhaseAsync(document, affectedIds, user, cancellationToken);
        await UpdateDocumentHeaderAsync(id, "borrow-return", payload, user, cancellationToken);
        var affectedSet = affectedIds.ToHashSet();
        var linesToPost = incomingByInstance.Values.Where(x => affectedSet.Contains(x.Instance.Id)).Select(x => x.Line).ToArray();
        if (linesToPost.Length > 0)
        {
            var postRequest = new BorrowReturnRequest { BorrowDocumentId = id, BorrowDocumentNo = document.DocumentNo, Returner = request.Returner, ReturnerCode = request.ReturnerCode, ReturnerName = request.ReturnerName, Purpose = request.Purpose, BorrowDepartment = request.BorrowDepartment, ApprovedBy = request.ApprovedBy, BorrowerPhone = request.BorrowerPhone, DepartmentOwner = request.DepartmentOwner, ReturnDate = request.ReturnDate, ReturnLocationBinCode = request.ReturnLocationBinCode, Note = request.Note, Lines = linesToPost };
            var post = await _borrowService.ReturnAsync(postRequest, user, cancellationToken);
            if (!post.Success) return ServiceResult<DocumentMutationResultDto>.Fail(post.Errors);
        }

        AddAuditLog(user, "Edit", nameof(BorrowDocument), id, document.DocumentNo, "Borrow return selectively edited.", payload.GetRawText());
        return Edited("borrow-return", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditRepairReceiveSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<RepairReceiveRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.RepairDocuments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Repair document not found.");

        var incoming = await ResolveLineInstancesAsync(request.Lines.Select(x => (x.ItemCode, x.SerialNumber)), cancellationToken);
        if (!incoming.Success) return ServiceResult<DocumentMutationResultDto>.Fail(incoming.Errors);
        var incomingByInstance = incoming.Data!.Zip(request.Lines, (i, l) => new { Instance = i, Line = l }).ToDictionary(x => x.Instance.Id);
        var returnedByInstance = document.Lines.Where(x => x.IsReturned).ToDictionary(x => x.ItemInstanceId);
        var affectedIds = returnedByInstance
            .Where(x => !incomingByInstance.ContainsKey(x.Key) || !Same(incomingByInstance[x.Key].Line.Note, x.Value.RepairResultNote))
            .Select(x => x.Key)
            .Concat(incomingByInstance.Keys.Where(x => !returnedByInstance.ContainsKey(x)))
            .Distinct()
            .ToArray();

        var deps = await FindLaterPhaseDependenciesAsync(nameof(RepairDocument), id, MovementActionType.ReceiveFromRepair, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await ReverseRepairReceivePhaseAsync(document, affectedIds, user, cancellationToken);
        await UpdateDocumentHeaderAsync(id, "repair-receive", payload, user, cancellationToken);
        var affectedSet = affectedIds.ToHashSet();
        var linesToPost = incomingByInstance.Values.Where(x => affectedSet.Contains(x.Instance.Id)).Select(x => x.Line).ToArray();
        if (linesToPost.Length > 0)
        {
            var postRequest = new RepairReceiveRequest { RepairDocumentId = id, RepairDocumentNo = document.DocumentNo, TargetWarehouseId = request.TargetWarehouseId, TargetBinCode = request.TargetBinCode, ResultNote = request.ResultNote, Note = request.Note, Lines = linesToPost };
            var post = await _repairService.ReceiveFromRepairAsync(postRequest, user, cancellationToken);
            if (!post.Success) return ServiceResult<DocumentMutationResultDto>.Fail(post.Errors);
        }

        AddAuditLog(user, "Edit", nameof(RepairDocument), id, document.DocumentNo, "Repair receive selectively edited.", payload.GetRawText());
        return Edited("repair-receive", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditInboundSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<InboundRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.InboundDocuments.Include(x => x.Lines).ThenInclude(x => x.Item).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Inbound document not found.");
        if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");

        var incoming = new List<(InboundLineRequest Line, Item Item, BinLocation Bin, InboundDocumentLine? Existing, ItemStatus Status, bool SideEffectsChanged)>();
        var errors = new List<string>();
        var existingByKey = document.Lines
            .Where(x => x.Item != null)
            .ToDictionary(x => SerialKey(x.Item!.ItemCode, x.SerialNumber), StringComparer.OrdinalIgnoreCase);
        var requestedKeys = request.Lines
            .Select(x => SerialKey(x.ItemCode, x.SerialNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var replacementRemovedInstanceIds = document.Lines
            .Where(x => x.Item != null && x.ItemInstanceId.HasValue && !requestedKeys.Contains(SerialKey(x.Item!.ItemCode, x.SerialNumber)))
            .Select(x => x.ItemInstanceId!.Value)
            .ToHashSet();
        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in request.Lines)
        {
            var item = await _db.Items.FirstOrDefaultAsync(x => x.ItemCode == line.ItemCode.Trim() && x.IsActive, cancellationToken);
            var bin = await _db.BinLocations.FirstOrDefaultAsync(x => x.BinCode == line.BinCode.Trim() && x.IsActive, cancellationToken);
            if (item == null) { errors.Add($"Item {line.ItemCode} not found."); continue; }
            if (bin == null) { errors.Add($"BinCode {line.BinCode} not found."); continue; }
            if (bin.WarehouseId != document.WarehouseId) { errors.Add($"BinCode {line.BinCode} does not belong to selected warehouse."); continue; }
            var key = SerialKey(item.ItemCode, line.SerialNumber);
            if (!incomingKeys.Add(key)) { errors.Add($"Serial {line.SerialNumber} is duplicated in this inbound document."); continue; }
            existingByKey.TryGetValue(key, out var existing);
            if (existing == null && await _db.ItemInstances.AnyAsync(x => x.ItemId == item.Id && x.SerialNumber == (line.SerialNumber ?? string.Empty).Trim(), cancellationToken))
            {
                errors.Add($"Serial {line.SerialNumber} already exists for item {item.ItemCode}.");
                continue;
            }
            var existingInstanceId = existing?.ItemInstanceId ?? 0;
            if ((existing == null || existing.BinLocationId != bin.Id) &&
                await _db.CurrentItemLocations.AnyAsync(x => x.BinLocationId == bin.Id && x.ItemInstanceId != existingInstanceId && !replacementRemovedInstanceIds.Contains(x.ItemInstanceId) && x.ItemInstance != null && x.ItemInstance.IsActive && x.ItemInstance.Status != ItemStatus.Lost && x.ItemInstance.Status != ItemStatus.Disposed, cancellationToken))
            {
                errors.Add($"Bin {bin.FullPath} already contains another active item.");
                continue;
            }
            var status = ResolveInboundStatus(line.Condition);
            var currentStatus = existing?.ItemInstanceId.HasValue == true
                ? await _db.ItemInstances.Where(x => x.Id == existing.ItemInstanceId.Value).Select(x => x.Status).FirstOrDefaultAsync(cancellationToken)
                : status;
            var sideEffectsChanged = existing == null ||
                                     existing.BinLocationId != bin.Id ||
                                     currentStatus != status;
            incoming.Add((line, item, bin, existing, status, sideEffectsChanged));
        }

        if (errors.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(errors);

        var removed = document.Lines.Where(x => x.Item != null && !incomingKeys.Contains(SerialKey(x.Item!.ItemCode, x.SerialNumber))).ToArray();
        var changedExisting = incoming.Where(x => x.Existing?.ItemInstanceId != null && x.SideEffectsChanged).Select(x => x.Existing!).ToArray();
        var affectedIds = removed.Concat(changedExisting).Where(x => x.ItemInstanceId.HasValue).Select(x => x.ItemInstanceId!.Value).Distinct().ToArray();
        var deps = await FindLaterHistoryDependenciesAsync(nameof(InboundDocument), id, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await UpdateDocumentHeaderAsync(id, "inbound", payload, user, cancellationToken);
        await ReverseInboundLinesAsync(document, removed, changedExisting, user, cancellationToken);

        foreach (var item in incoming.Where(x => x.Existing != null && !x.SideEffectsChanged))
        {
            var instance = item.Existing!.ItemInstanceId.HasValue
                ? await _db.ItemInstances.FirstOrDefaultAsync(x => x.Id == item.Existing.ItemInstanceId.Value, cancellationToken)
                : null;
            if (instance != null)
            {
                instance.MT = item.Line.MT;
                instance.OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim();
                instance.UpdatedAt = _clock.UtcNow;
                instance.UpdatedBy = user.UserName;
            }

            item.Existing.Quantity = 1;
            item.Existing.Condition = string.IsNullOrEmpty(item.Line.Condition) ? "Normal" : item.Line.Condition;
            item.Existing.Note = item.Line.Note;
            item.Existing.UpdatedAt = _clock.UtcNow;
            item.Existing.UpdatedBy = user.UserName;

            var log = item.Existing.ItemInstanceId.HasValue
                ? await _db.InboundDocumentLogs.FirstOrDefaultAsync(x => x.InboundDocumentId == document.Id && x.ItemInstanceId == item.Existing.ItemInstanceId.Value, cancellationToken)
                : null;
            if (log != null)
            {
                log.Note = item.Line.Note;
                log.ReceiverPhone = request.ReceiverPhone;
                log.ReceiverDepartment = request.ReceiverDepartment;
                log.DepartmentOwner = request.DepartmentOwner;
            }
        }

        foreach (var item in incoming.Where(x => x.Existing == null || x.SideEffectsChanged))
        {
            var instance = item.Existing?.ItemInstanceId == null
                ? new ItemInstance
                {
                    ItemId = item.Item.Id,
                    SerialNumber = string.IsNullOrWhiteSpace(item.Line.SerialNumber) ? null : item.Line.SerialNumber.Trim(),
                    Barcode = string.IsNullOrWhiteSpace(item.Line.SerialNumber) ? null : item.Line.SerialNumber.Trim(),
                    MT = item.Line.MT,
                    DocumentNo = document.DocumentNo,
                    TrackingType = ItemTrackingType.LocationTracked,
                    OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim(),
                    CreatedAt = request.DocumentDate,
                    CreatedBy = user.UserName
                }
                : await _db.ItemInstances.FirstAsync(x => x.Id == item.Existing.ItemInstanceId.Value, cancellationToken);

            instance.Status = item.Status;
            instance.IsActive = true;
            instance.MT = item.Line.MT;
            instance.OwnerName = string.IsNullOrWhiteSpace(request.OwnerName) ? null : request.OwnerName.Trim();
            instance.UpdatedAt = _clock.UtcNow;
            instance.UpdatedBy = user.UserName;
            if (item.Existing?.ItemInstanceId == null) _db.ItemInstances.Add(instance);
            await _db.SaveChangesAsync(cancellationToken);

            var lineEntity = item.Existing ?? new InboundDocumentLine { InboundDocumentId = document.Id, CreatedAt = request.DocumentDate, CreatedBy = user.UserName };
            lineEntity.ItemId = item.Item.Id;
            lineEntity.ItemInstanceId = instance.Id;
            lineEntity.SerialNumber = instance.SerialNumber;
            lineEntity.Barcode = instance.Barcode;
            lineEntity.Quantity = 1;
            lineEntity.BinLocationId = item.Bin.Id;
            lineEntity.Condition = string.IsNullOrEmpty(item.Line.Condition) ? "Normal" : item.Line.Condition;
            lineEntity.Note = item.Line.Note;
            if (item.Existing == null) _db.InboundDocumentLines.Add(lineEntity);

            _db.CurrentItemLocations.Add(new CurrentItemLocation
            {
                ItemInstanceId = instance.Id,
                LocationType = LocationType.BinLocation,
                WarehouseId = document.WarehouseId,
                BinLocationId = item.Bin.Id,
                ReferenceDocumentType = nameof(InboundDocument),
                ReferenceDocumentId = document.Id,
                ReferenceDocumentNo = document.DocumentNo,
                UpdatedLocationAt = request.DocumentDate,
                UpdatedLocationBy = user.UserName,
                CreatedAt = request.DocumentDate,
                CreatedBy = user.UserName
            });
            AddInboundSideEffects(document, lineEntity, instance, item.Bin, request, user);
        }

        var itemIds = document.Lines.Select(x => x.ItemId).Concat(incoming.Select(x => x.Item.Id)).Distinct().ToArray();
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateLocationTrackedStockAsync(itemIds, cancellationToken);
        AddAuditLog(user, "Edit", nameof(InboundDocument), id, document.DocumentNo, "Inbound document selectively edited.", payload.GetRawText());
        return Edited("inbound", id, document.DocumentNo);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> EditAdjustmentSelectiveAsync(int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<AdjustmentRequest>(payload.GetRawText(), JsonOptions);
        var document = await _db.AdjustmentDocuments
            .Include(x => x.Lines)
            .ThenInclude(x => x.ItemInstance)!.ThenInclude(x => x!.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request == null || document == null) return ServiceResult<DocumentMutationResultDto>.Fail("Adjustment document not found.");
        if (request.WarehouseId != document.WarehouseId) return ServiceResult<DocumentMutationResultDto>.Fail("Changing warehouse requires line-level selective mutation support.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return ServiceResult<DocumentMutationResultDto>.Fail("Adjustment reason is required.");

        var errors = new List<string>();
        var incoming = new List<(AdjustmentLineRequest Line, ItemInstance Instance, BinLocation? TargetBin, ExternalParty? TargetParty, AdjustmentDocumentLine? Existing, bool SideEffectsChanged)>();
        var existingByKey = document.Lines
            .Where(x => x.ItemInstance?.Item != null)
            .GroupBy(x => SerialKey(x.ItemInstance!.Item!.ItemCode, x.ItemInstance.SerialNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(l => l.Id).First(), StringComparer.OrdinalIgnoreCase);
        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in request.Lines)
        {
            if (line.NewStatus == ItemStatus.Replacement.ToString())
            {
                errors.Add("Replacement adjustment line edit requires explicit rebuild/recovery flow.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(line.Reason)) { errors.Add("Line adjustment reason is required."); continue; }
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, cancellationToken);
            if (instance == null) { errors.Add($"Item {line.ItemCode} with serial {line.SerialNumber} not found."); continue; }
            var key = SerialKey(line.ItemCode, line.SerialNumber);
            if (!incomingKeys.Add(key)) { errors.Add($"Item {line.ItemCode}/{line.SerialNumber} is duplicated in this adjustment document."); continue; }

            BinLocation? targetBin = null;
            if (!string.IsNullOrWhiteSpace(line.TargetBinCode))
            {
                targetBin = await _db.BinLocations.FirstOrDefaultAsync(x => x.BinCode == line.TargetBinCode.Trim() && x.IsActive, cancellationToken);
                if (targetBin == null) { errors.Add($"Target bin {line.TargetBinCode} not found."); continue; }
                if (targetBin.WarehouseId != document.WarehouseId) { errors.Add($"Target bin {line.TargetBinCode} does not belong to warehouse."); continue; }
                if (await _db.CurrentItemLocations.AnyAsync(x => x.BinLocationId == targetBin.Id && x.ItemInstanceId != instance.Id && x.ItemInstance != null && x.ItemInstance.IsActive && x.ItemInstance.Status != ItemStatus.Lost && x.ItemInstance.Status != ItemStatus.Disposed, cancellationToken))
                {
                    errors.Add($"Target bin {targetBin.FullPath} already contains another active item.");
                    continue;
                }
            }

            ExternalParty? targetParty = null;
            if (!string.IsNullOrWhiteSpace(line.TargetExternalPartyCode))
            {
                targetParty = await _db.ExternalParties.FirstOrDefaultAsync(x => x.PartyCode == line.TargetExternalPartyCode.Trim() && x.IsActive, cancellationToken);
                if (targetParty == null) { errors.Add($"External party {line.TargetExternalPartyCode} not found."); continue; }
            }

            existingByKey.TryGetValue(key, out var existing);
            var sideEffectsChanged = existing == null ||
                                     existing.ItemInstanceId != instance.Id ||
                                     existing.NewStatus.ToString() != line.NewStatus ||
                                     existing.TargetBinLocationId != targetBin?.Id ||
                                     existing.TargetExternalPartyId != targetParty?.Id;
            incoming.Add((line, instance, targetBin, targetParty, existing, sideEffectsChanged));
        }

        if (errors.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(errors);

        var removed = document.Lines
            .Where(x => x.ItemInstance?.Item != null && !incomingKeys.Contains(SerialKey(x.ItemInstance!.Item!.ItemCode, x.ItemInstance.SerialNumber)))
            .ToArray();
        var changedExisting = incoming
            .Where(x => x.Existing != null && x.SideEffectsChanged)
            .Select(x => x.Existing!)
            .ToArray();
        var affectedIds = removed.Concat(changedExisting).Select(x => x.ItemInstanceId).Concat(incoming.Where(x => x.Existing == null).Select(x => x.Instance.Id)).Distinct().ToArray();
        var deps = await FindLaterHistoryDependenciesAsync(nameof(AdjustmentDocument), id, affectedIds, cancellationToken);
        if (deps.Count > 0) return ServiceResult<DocumentMutationResultDto>.Fail(deps);

        await UpdateDocumentHeaderAsync(id, "adjustment", payload, user, cancellationToken);
        await ReverseAdjustmentLinesAsync(document, removed.Concat(changedExisting).ToArray(), user, cancellationToken);

        foreach (var item in incoming.Where(x => x.Existing != null && !x.SideEffectsChanged))
        {
            item.Existing!.Reason = item.Line.Reason;
            item.Existing.UpdatedAt = _clock.UtcNow;
            item.Existing.UpdatedBy = user.UserName;
            var log = await _db.AdjustmentDocumentLogs.FirstOrDefaultAsync(x => x.AdjustmentDocumentId == document.Id && x.ItemInstanceId == item.Instance.Id && x.Action == "Adjust", cancellationToken);
            if (log != null)
            {
                log.Reason = item.Line.Reason;
                log.Note = item.Line.Reason;
            }
        }

        var addResult = await AddAdjustmentEffectsAsync(document, incoming.Where(x => x.Existing == null || x.SideEffectsChanged).ToArray(), user, cancellationToken);
        if (!addResult.Success) return addResult;

        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateLocationTrackedStockAsync(incoming.Select(x => x.Instance.ItemId), cancellationToken);
        AddAuditLog(user, "Edit", nameof(AdjustmentDocument), id, document.DocumentNo, "Adjustment document selectively edited.", payload.GetRawText());
        return Edited("adjustment", id, document.DocumentNo);
    }

    private async Task<ServiceResult<IReadOnlyCollection<(MoveLocationLineRequest Line, ItemInstance Instance, BinLocation TargetBin)>>> PrepareMoveLinesAsync(MoveLocationRequest request, CurrentUserContext user, CancellationToken ct)
    {
        var errors = new List<string>();
        var rows = new List<(MoveLocationLineRequest Line, ItemInstance Instance, BinLocation TargetBin)>();
        var seenInstances = new HashSet<int>();
        var seenBins = new HashSet<int>();
        foreach (var line in request.Lines.Where(x => !string.IsNullOrWhiteSpace(x.ItemCode) && !string.IsNullOrWhiteSpace(x.SerialNumber)))
        {
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, ct);
            var targetBin = await _db.BinLocations.FirstOrDefaultAsync(x => x.BinCode == line.TargetBinCode.Trim() && x.IsActive, ct);
            if (instance == null) { errors.Add($"Item {line.ItemCode} with serial {line.SerialNumber} not found."); continue; }
            if (targetBin == null) { errors.Add($"BinCode {line.TargetBinCode} not found."); continue; }
            if (targetBin.WarehouseId != request.WarehouseId) { errors.Add($"BinCode {line.TargetBinCode} does not belong to selected warehouse."); continue; }
            if (!seenInstances.Add(instance.Id)) errors.Add($"Item instance {line.ItemCode}/{line.SerialNumber} is duplicated in this request.");
            if (!seenBins.Add(targetBin.Id)) errors.Add($"Target bin {targetBin.BinCode} is already used in another line.");
            rows.Add((line, instance, targetBin));
        }

        if (rows.Count == 0) errors.Add("At least one item is required.");
        return errors.Count > 0
            ? ServiceResult<IReadOnlyCollection<(MoveLocationLineRequest Line, ItemInstance Instance, BinLocation TargetBin)>>.Fail(errors)
            : ServiceResult<IReadOnlyCollection<(MoveLocationLineRequest Line, ItemInstance Instance, BinLocation TargetBin)>>.Ok(rows);
    }

    private async Task<ServiceResult<IReadOnlyCollection<ItemInstance>>> ResolveLineInstancesAsync(IEnumerable<(string ItemCode, string SerialNumber)> lines, CancellationToken ct)
    {
        var errors = new List<string>();
        var instances = new List<ItemInstance>();
        var seen = new HashSet<int>();
        foreach (var line in lines)
        {
            var instance = await FindInstanceByCodeAsync(line.ItemCode, line.SerialNumber, ct);
            if (instance == null) { errors.Add($"Item {line.ItemCode} with serial {line.SerialNumber} not found."); continue; }
            if (!seen.Add(instance.Id)) errors.Add($"Item instance {line.ItemCode}/{line.SerialNumber} is duplicated in this request.");
            instances.Add(instance);
        }

        if (instances.Count == 0) errors.Add("At least one item is required.");
        return errors.Count > 0
            ? ServiceResult<IReadOnlyCollection<ItemInstance>>.Fail(errors)
            : ServiceResult<IReadOnlyCollection<ItemInstance>>.Ok(instances);
    }

    private async Task<ItemInstance?> FindInstanceByCodeAsync(string? itemCode, string? serialNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(serialNumber)) return null;
        var code = itemCode.Trim();
        var sn = serialNumber.Trim();
        return await _db.ItemInstances.Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Item != null && x.Item.ItemCode == code && x.SerialNumber == sn, ct);
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> AddMoveEffectsAsync(MoveDocument document, IReadOnlyCollection<(MoveLocationLineRequest Line, ItemInstance Instance, BinLocation TargetBin)> rows, CurrentUserContext user, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            var current = await _db.CurrentItemLocations
                .Include(x => x.BinLocation)
                .Include(x => x.ExternalParty)
                .Include(x => x.Warehouse)
                .FirstOrDefaultAsync(x => x.ItemInstanceId == row.Instance.Id, ct);
            if (current == null) return ServiceResult<DocumentMutationResultDto>.Fail($"Current location for item instance {row.Instance.Id} does not exist.");
            if (!current.WarehouseId.HasValue || current.WarehouseId.Value != document.WarehouseId || !current.BinLocationId.HasValue)
                return ServiceResult<DocumentMutationResultDto>.Fail($"Item instance {row.Line.ItemCode}/{row.Line.SerialNumber} does not belong to selected warehouse.");
            if (!IsInWarehouse(row.Instance.Status))
                return ServiceResult<DocumentMutationResultDto>.Fail($"Item instance {row.Line.ItemCode}/{row.Line.SerialNumber} cannot be moved.");
            //if (await _db.CurrentItemLocations.AnyAsync(x => x.BinLocationId == row.TargetBin.Id && x.ItemInstanceId != row.Instance.Id && x.ItemInstance != null && x.ItemInstance.IsActive && x.ItemInstance.Status != ItemStatus.Lost && x.ItemInstance.Status != ItemStatus.Disposed, ct))
            //    return ServiceResult<DocumentMutationResultDto>.Fail($"Target bin {row.TargetBin.FullPath} already contains another active item.");

            var fromBin = current.BinLocationId;
            var fromDisplay = LocationDisplayText(current);
            current.LocationType = LocationType.BinLocation;
            current.WarehouseId = document.WarehouseId;
            current.BinLocationId = row.TargetBin.Id;
            current.ExternalPartyId = null;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(MoveDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = document.DocumentDate;
            current.UpdatedLocationBy = user.UserName;

            _db.MoveDocumentLines.Add(new MoveDocumentLine { MoveDocumentId = document.Id, ItemInstanceId = row.Instance.Id, FromBinLocationId = fromBin, TargetBinLocationId = row.TargetBin.Id, Note = row.Line.Note, CreatedAt = document.DocumentDate, CreatedBy = user.UserName });
            _db.ItemMovementHistories.Add(new ItemMovementHistory { ItemInstanceId = row.Instance.Id, ActionType = MovementActionType.MoveLocation, FromLocationType = LocationType.BinLocation, FromLocationId = fromBin, FromLocationDisplay = fromDisplay, ToLocationType = LocationType.BinLocation, ToLocationId = row.TargetBin.Id, ToLocationDisplay = row.TargetBin.FullPath, OldStatus = row.Instance.Status, NewStatus = row.Instance.Status, DocumentType = nameof(MoveDocument), DocumentId = document.Id, DocumentNo = document.DocumentNo, Note = row.Line.Note, PerformedAt = document.DocumentDate, PerformedBy = user.UserName });
            _db.InventoryTransactions.Add(new InventoryTransaction { TransactionType = InventoryTransactionType.Move, ItemId = row.Instance.ItemId, ItemInstanceId = row.Instance.Id, WarehouseId = document.WarehouseId, BinLocationId = row.TargetBin.Id, QuantityDelta = 0, StatusAfter = row.Instance.Status, DocumentType = nameof(MoveDocument), DocumentId = document.Id, DocumentNo = document.DocumentNo, PostedAt = document.DocumentDate, PostedBy = user.UserName });
        }

        await _db.SaveChangesAsync(ct);
        await RecalculateLocationTrackedStockAsync(rows.Select(x => x.Instance.ItemId), ct);
        return Edited("move", document.Id, document.DocumentNo);
    }

    private async Task ReverseLocationTrackedLinesAsync(string documentType, int documentId, IReadOnlyCollection<int> instanceIds, string documentNo, Func<IReadOnlyCollection<int>, IQueryable<object>>? logCleanup, CancellationToken ct)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        var itemIds = await _db.ItemInstances.Where(x => ids.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(ct);
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == documentType && x.DocumentId == documentId && x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value)));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == documentType && x.DocumentId == documentId && ids.Contains(x.ItemInstanceId)));
        if (documentType == nameof(MoveDocument)) _db.MoveDocumentLines.RemoveRange(_db.MoveDocumentLines.Where(x => x.MoveDocumentId == documentId && ids.Contains(x.ItemInstanceId)));
        if (documentType == nameof(BorrowDocument)) _db.BorrowDocumentLines.RemoveRange(_db.BorrowDocumentLines.Where(x => x.BorrowDocumentId == documentId && ids.Contains(x.ItemInstanceId) && !x.IsReturned));
        if (documentType == nameof(RepairDocument)) _db.RepairDocumentLines.RemoveRange(_db.RepairDocumentLines.Where(x => x.RepairDocumentId == documentId && ids.Contains(x.ItemInstanceId) && !x.IsReturned));
        if (logCleanup != null) _db.RemoveRange(logCleanup(ids));
        await CleanupPostSideEffectsAsync(documentType, documentId, documentNo, ct);
        await _db.SaveChangesAsync(ct);
        await RebuildLocationTrackedInstancesAsync(ids, ct);
        await RecalculateLocationTrackedStockAsync(itemIds, ct);
    }

    private async Task ReverseReturnPhaseAsync(BorrowDocument document, IReadOnlyCollection<int> instanceIds, CurrentUserContext user, CancellationToken ct)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        foreach (var line in document.Lines.Where(x => ids.Contains(x.ItemInstanceId)))
        {
            line.IsReturned = false; line.ReturnCondition = null; line.ReturnedAt = null; line.TargetBinLocationId = null; line.UpdatedAt = _clock.UtcNow; line.UpdatedBy = user.UserName;
        }
        _db.BorrowDocumentLogs.RemoveRange(_db.BorrowDocumentLogs.Where(x => x.BorrowDocumentId == document.Id && x.Action == "BorrowReturn" && ids.Contains(x.ItemInstanceId)));
        await ReverseLocationTrackedPhaseAsync(nameof(BorrowDocument), document.Id, MovementActionType.ReturnBorrowed, InventoryTransactionType.BorrowReturn, ids, document.DocumentNo, ct);
    }

    private async Task ReverseRepairReceivePhaseAsync(RepairDocument document, IReadOnlyCollection<int> instanceIds, CurrentUserContext user, CancellationToken ct)
    {
        var ids = instanceIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        foreach (var line in document.Lines.Where(x => ids.Contains(x.ItemInstanceId)))
        {
            line.IsReturned = false; line.TargetBinLocationId = null; line.NewSerialNumber = null; line.UpdatedAt = _clock.UtcNow; line.UpdatedBy = user.UserName;
        }
        _db.RepairDocumentLogs.RemoveRange(_db.RepairDocumentLogs.Where(x => x.RepairDocumentId == document.Id && x.Action == "RepairReceive" && ids.Contains(x.ItemInstanceId)));
        await ReverseLocationTrackedPhaseAsync(nameof(RepairDocument), document.Id, MovementActionType.ReceiveFromRepair, InventoryTransactionType.RepairReceive, ids, document.DocumentNo, ct);
    }

    private async Task ReverseLocationTrackedPhaseAsync(string documentType, int documentId, MovementActionType actionType, InventoryTransactionType transactionType, IReadOnlyCollection<int> instanceIds, string documentNo, CancellationToken ct)
    {
        var ids = instanceIds.Distinct().ToArray();
        var itemIds = await _db.ItemInstances.Where(x => ids.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(ct);
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == documentType && x.DocumentId == documentId && x.TransactionType == transactionType && x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value)));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == documentType && x.DocumentId == documentId && x.ActionType == actionType && ids.Contains(x.ItemInstanceId)));
        await CleanupPostSideEffectsAsync(documentType, documentId, documentNo, ct);
        await _db.SaveChangesAsync(ct);
        await RebuildLocationTrackedInstancesAsync(ids, ct);
        await RecalculateLocationTrackedStockAsync(itemIds, ct);
    }

    private async Task ReverseInboundLinesAsync(InboundDocument document, IReadOnlyCollection<InboundDocumentLine> removed, IReadOnlyCollection<InboundDocumentLine> changed, CurrentUserContext user, CancellationToken ct)
    {
        var removedIds = removed.Where(x => x.ItemInstanceId.HasValue).Select(x => x.ItemInstanceId!.Value).ToArray();
        var changedIds = changed.Where(x => x.ItemInstanceId.HasValue).Select(x => x.ItemInstanceId!.Value).ToArray();
        var allIds = removedIds.Concat(changedIds).Distinct().ToArray();
        if (allIds.Length == 0) return;
        _db.InboundDocumentLogs.RemoveRange(_db.InboundDocumentLogs.Where(x => x.InboundDocumentId == document.Id && allIds.Contains(x.ItemInstanceId)));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(InboundDocument) && x.DocumentId == document.Id && x.ItemInstanceId.HasValue && allIds.Contains(x.ItemInstanceId.Value)));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(InboundDocument) && x.DocumentId == document.Id && allIds.Contains(x.ItemInstanceId)));
        _db.CurrentItemLocations.RemoveRange(_db.CurrentItemLocations.Where(x => allIds.Contains(x.ItemInstanceId)));
        _db.InboundDocumentLines.RemoveRange(removed);
        _db.ItemInstances.RemoveRange(_db.ItemInstances.Where(x => removedIds.Contains(x.Id)));
        await CleanupPostSideEffectsAsync(nameof(InboundDocument), document.Id, document.DocumentNo, ct);
    }

    private async Task ReverseAdjustmentLinesAsync(AdjustmentDocument document, IReadOnlyCollection<AdjustmentDocumentLine> lines, CurrentUserContext user, CancellationToken ct)
    {
        var ids = lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        if (ids.Length == 0) return;

        var itemIds = await _db.ItemInstances.Where(x => ids.Contains(x.Id)).Select(x => x.ItemId).Distinct().ToArrayAsync(ct);
        _db.AdjustmentDocumentLogs.RemoveRange(_db.AdjustmentDocumentLogs.Where(x => x.AdjustmentDocumentId == document.Id && ids.Contains(x.ItemInstanceId)));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == document.Id && x.ItemInstanceId.HasValue && ids.Contains(x.ItemInstanceId.Value)));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == document.Id && ids.Contains(x.ItemInstanceId)));
        _db.AdjustmentDocumentLines.RemoveRange(lines);
        await CleanupPostSideEffectsAsync(nameof(AdjustmentDocument), document.Id, document.DocumentNo, ct);
        await _db.SaveChangesAsync(ct);
        await RebuildLocationTrackedInstancesAsync(ids, ct);
        await RecalculateLocationTrackedStockAsync(itemIds, ct);
    }

    private async Task RestoreAdjustmentFallbacksAsync(IReadOnlyCollection<AdjustmentDocumentLine> lines, CurrentUserContext user, CancellationToken ct)
    {
        var ids = lines.Select(x => x.ItemInstanceId).Distinct().ToArray();
        if (ids.Length == 0) return;

        var hasHistoryIds = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId))
            .Select(x => x.ItemInstanceId)
            .Distinct()
            .ToArrayAsync(ct);
        var hasHistory = hasHistoryIds.ToHashSet();
        var fallbackLines = lines
            .GroupBy(x => x.ItemInstanceId)
            .Select(x => x.OrderBy(l => l.Id).First())
            .Where(x => !hasHistory.Contains(x.ItemInstanceId))
            .ToArray();
        if (fallbackLines.Length == 0) return;

        var fallbackIds = fallbackLines.Select(x => x.ItemInstanceId).ToArray();
        var instances = await _db.ItemInstances.Where(x => fallbackIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var locations = await _db.CurrentItemLocations.Where(x => fallbackIds.Contains(x.ItemInstanceId)).ToDictionaryAsync(x => x.ItemInstanceId, ct);
        var binIds = fallbackLines.Where(x => x.FromBinLocationId.HasValue).Select(x => x.FromBinLocationId!.Value).Distinct().ToArray();
        var binWarehouseMap = await _db.BinLocations.Where(x => binIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.WarehouseId, ct);

        foreach (var line in fallbackLines)
        {
            if (!instances.TryGetValue(line.ItemInstanceId, out var instance)) continue;

            instance.Status = line.OldStatus;
            instance.IsActive = line.OldStatus != ItemStatus.Replacement && line.OldStatus != ItemStatus.Disposed;
            instance.UpdatedAt = _clock.UtcNow;
            instance.UpdatedBy = user.UserName;

            if (!locations.TryGetValue(line.ItemInstanceId, out var location))
            {
                location = new CurrentItemLocation
                {
                    ItemInstanceId = line.ItemInstanceId,
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = user.UserName
                };
                _db.CurrentItemLocations.Add(location);
            }

            location.LocationType = line.FromBinLocationId.HasValue ? LocationType.BinLocation : LocationType.Unknown;
            location.BinLocationId = line.FromBinLocationId;
            location.WarehouseId = line.FromBinLocationId.HasValue && binWarehouseMap.TryGetValue(line.FromBinLocationId.Value, out var warehouseId) ? warehouseId : null;
            location.ExternalPartyId = null;
            location.ExternalLocationText = null;
            location.ReferenceDocumentType = null;
            location.ReferenceDocumentId = null;
            location.ReferenceDocumentNo = null;
            location.UpdatedLocationAt = _clock.UtcNow;
            location.UpdatedLocationBy = user.UserName;
        }
    }

    private async Task<ServiceResult<DocumentMutationResultDto>> AddAdjustmentEffectsAsync(
        AdjustmentDocument document,
        IReadOnlyCollection<(AdjustmentLineRequest Line, ItemInstance Instance, BinLocation? TargetBin, ExternalParty? TargetParty, AdjustmentDocumentLine? Existing, bool SideEffectsChanged)> rows,
        CurrentUserContext user,
        CancellationToken ct)
    {
        foreach (var row in rows)
        {
            var current = await _db.CurrentItemLocations
                .Include(x => x.BinLocation)
                .Include(x => x.ExternalParty)
                .Include(x => x.Warehouse)
                .FirstOrDefaultAsync(x => x.ItemInstanceId == row.Instance.Id, ct);
            if (current == null) return ServiceResult<DocumentMutationResultDto>.Fail($"Current location for item instance {row.Instance.Id} does not exist.");

            var oldStatus = row.Instance.Status;
            var fromLocationType = current.LocationType;
            var fromWarehouseId = current.WarehouseId;
            var fromBinLocationId = current.BinLocationId;
            var fromDisplay = LocationDisplayText(current);
            var status = ResolveStatus(row.Line.NewStatus);

            row.Instance.Status = status;
            row.Instance.UpdatedAt = _clock.UtcNow;
            row.Instance.UpdatedBy = user.UserName;

            current.LocationType = row.TargetParty != null ? LocationType.Borrower : (row.TargetBin != null ? LocationType.BinLocation : LocationType.Unknown);
            current.WarehouseId = row.TargetBin != null ? row.TargetBin.WarehouseId : null;
            current.BinLocationId = row.TargetBin?.Id;
            current.ExternalPartyId = row.TargetParty?.Id;
            current.ExternalLocationText = null;
            current.ReferenceDocumentType = nameof(AdjustmentDocument);
            current.ReferenceDocumentId = document.Id;
            current.ReferenceDocumentNo = document.DocumentNo;
            current.UpdatedLocationAt = document.DocumentDate;
            current.UpdatedLocationBy = user.UserName;

            _db.AdjustmentDocumentLines.Add(new AdjustmentDocumentLine
            {
                AdjustmentDocumentId = document.Id,
                ItemInstanceId = row.Instance.Id,
                OldStatus = oldStatus,
                NewStatus = status,
                FromBinLocationId = fromBinLocationId,
                TargetBinLocationId = row.TargetBin?.Id,
                TargetExternalPartyId = row.TargetParty?.Id,
                Reason = row.Line.Reason,
                CreatedAt = document.DocumentDate,
                CreatedBy = user.UserName
            });

            if (fromWarehouseId.HasValue && fromBinLocationId.HasValue)
            {
                await ApplyStockDeltaAsync(fromWarehouseId.Value, fromBinLocationId, row.Instance.ItemId, oldStatus, -1, user, ct);
            }

            if (row.TargetBin != null)
            {
                await ApplyStockDeltaAsync(row.TargetBin.WarehouseId, row.TargetBin.Id, row.Instance.ItemId, status, 1, user, ct);
            }

            var toDisplay = row.TargetBin?.FullPath ?? (row.TargetParty?.Name ?? "Adjusted location");
            _db.ItemMovementHistories.Add(new ItemMovementHistory
            {
                ItemInstanceId = row.Instance.Id,
                ActionType = MovementActionType.Adjustment,
                FromLocationType = fromLocationType,
                FromLocationId = fromBinLocationId,
                FromLocationDisplay = fromDisplay,
                ToLocationType = current.LocationType,
                ToLocationId = current.BinLocationId ?? current.ExternalPartyId,
                ToLocationDisplay = toDisplay,
                OldStatus = oldStatus,
                NewStatus = status,
                DocumentType = nameof(AdjustmentDocument),
                DocumentId = document.Id,
                DocumentNo = document.DocumentNo,
                Note = row.Line.Reason,
                PerformedAt = document.DocumentDate,
                PerformedBy = user.UserName
            });
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionType = InventoryTransactionType.Adjustment,
                ItemId = row.Instance.ItemId,
                ItemInstanceId = row.Instance.Id,
                WarehouseId = document.WarehouseId,
                BinLocationId = row.TargetBin?.Id,
                QuantityDelta = 0,
                StatusAfter = status,
                DocumentType = nameof(AdjustmentDocument),
                DocumentId = document.Id,
                DocumentNo = document.DocumentNo,
                PostedAt = document.DocumentDate,
                PostedBy = user.UserName
            });
            _db.AdjustmentDocumentLogs.Add(new AdjustmentDocumentLog
            {
                AdjustmentDocumentId = document.Id,
                ItemInstanceId = row.Instance.Id,
                Action = "Adjust",
                OldStatus = oldStatus.ToString(),
                NewStatus = row.Line.NewStatus.ToString(),
                OldLocationText = fromDisplay,
                NewLocationText = toDisplay,
                Reason = row.Line.Reason,
                PerformedBy = user.UserName,
                Timestamp = document.DocumentDate,
                Note = row.Line.Reason
            });
        }

        return Edited("adjustment", document.Id, document.DocumentNo);
    }

    private void AddInboundSideEffects(InboundDocument document, InboundDocumentLine line, ItemInstance instance, BinLocation bin, InboundRequest request, CurrentUserContext user)
    {
        _db.ItemMovementHistories.Add(new ItemMovementHistory { ItemInstanceId = instance.Id, ActionType = MovementActionType.Inbound, FromLocationType = null, FromLocationId = null, FromLocationDisplay = "Supplier", ToLocationType = LocationType.BinLocation, ToLocationId = bin.Id, ToLocationDisplay = bin.FullPath, OldStatus = ItemStatus.Reserved, NewStatus = instance.Status, DocumentType = nameof(InboundDocument), DocumentId = document.Id, DocumentNo = document.DocumentNo, Note = line.Note, PerformedAt = request.DocumentDate, PerformedBy = user.UserName });
        _db.InventoryTransactions.Add(new InventoryTransaction { TransactionType = InventoryTransactionType.Inbound, ItemId = instance.ItemId, ItemInstanceId = instance.Id, WarehouseId = document.WarehouseId, BinLocationId = bin.Id, QuantityDelta = 1, StatusAfter = instance.Status, DocumentType = nameof(InboundDocument), DocumentId = document.Id, DocumentNo = document.DocumentNo, PostedAt = request.DocumentDate, PostedBy = user.UserName });
        _db.InboundDocumentLogs.Add(new InboundDocumentLog { InboundDocumentId = document.Id, ItemInstanceId = instance.Id, Action = "InboundReceive", OldStatus = "Reserved", NewStatus = instance.Status.ToString(), Receiver = $"{request.ReceiverCode}-{request.ReceiverName}", ReceiverPhone = request.ReceiverPhone, ReceiverDepartment = request.ReceiverDepartment, DepartmentOwner = request.DepartmentOwner, OldLocationText = "Supplier", NewLocationText = bin.FullPath, PerformedBy = user.UserName, Timestamp = request.DocumentDate, Note = line.Note });
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

        // FIX A: Flush staged deletes to DB within the outer transaction before calling
        // RepostAsync. Operation services use AsNoTracking queries (e.g. FindInboundDocumentByCodeAsync)
        // which bypass EF's change tracker and read directly from DB. Without this flush,
        // the deleted document is still visible in the DB and triggers the APPEND PATH,
        // causing lines to be appended to a document that is being deleted in the same batch.
        await _db.SaveChangesAsync(cancellationToken);

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
        await _db.SaveChangesAsync(cancellationToken);
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
        var fallbackLines = document.Lines.ToArray();
        _db.AdjustmentDocumentLogs.RemoveRange(_db.AdjustmentDocumentLogs.Where(x => x.AdjustmentDocumentId == id));
        _db.InventoryTransactions.RemoveRange(_db.InventoryTransactions.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == id));
        _db.ItemMovementHistories.RemoveRange(_db.ItemMovementHistories.Where(x => x.DocumentType == nameof(AdjustmentDocument) && x.DocumentId == id));
        _db.AdjustmentDocumentLines.RemoveRange(document.Lines);
        _db.AdjustmentDocuments.Remove(document);
        await RebuildLocationTrackedInstancesAsync(instanceIds, cancellationToken);
        await RestoreAdjustmentFallbacksAsync(fallbackLines, user, cancellationToken);
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
        // FIX C: Remove Notification records tied to the BorrowReturn action.
        // Previously missing — caused orphan notifications pointing to a deleted return phase.
        await CleanupPostSideEffectsAsync(nameof(BorrowDocument), id, document.DocumentNo, cancellationToken);
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
        // FIX D: Remove Notification records tied to the RepairReceive action.
        // Previously missing — caused orphan notifications pointing to a deleted receive phase.
        await CleanupPostSideEffectsAsync(nameof(RepairDocument), id, document.DocumentNo, cancellationToken);
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

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var key in lineKeys)
        {
            await RebuildQuantityInstanceAsync(document.WarehouseId, key.ItemId, key.SnCode, cancellationToken);
        }

        return Deleted(type, document.Id, document.DocumentNo);
    }

    private async Task RebuildLocationTrackedInstancesAsync(IEnumerable<int> instanceIds, CancellationToken cancellationToken)
    {
        await _rollbackService.RestoreLocationTrackedInstancesAsync(instanceIds, cancellationToken);
    }

    private async Task RebuildQuantityInstanceAsync(int warehouseId, int itemId, string snCode, CancellationToken cancellationToken)
    {
        var balances = await _db.QuantityStockBalances.Where(x => x.ItemId == itemId && x.SnCode == snCode).ToListAsync(cancellationToken);
        _db.QuantityStockBalances.RemoveRange(balances);
        await _db.SaveChangesAsync(cancellationToken);

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
            _db.ItemInstances.Remove(instance);
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

    private async Task ApplyStockDeltaAsync(int warehouseId, int? binLocationId, int itemId, ItemStatus status, decimal delta, CurrentUserContext user, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var balance = _db.StockBalances.Local.FirstOrDefault(x => x.WarehouseId == warehouseId &&
        x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status);

        if (balance == null)
        {
            balance = await _db.StockBalances.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId &&
            x.BinLocationId == binLocationId && x.ItemId == itemId && x.Status == status, cancellationToken);
        }

        if (balance == null)
        {
            balance = new StockBalance
            {
                WarehouseId = warehouseId,
                BinLocationId = binLocationId,
                ItemId = itemId,
                Status = status,
                Quantity = 0,
                CreatedAt = now,
                CreatedBy = user.UserName
            };
            _db.StockBalances.Add(balance);
        }

        balance.Quantity += delta;
        balance.UpdatedAt = now;
        balance.UpdatedBy = user.UserName;
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
            .Select(g => new { ItemInstanceId = g.Key, LastId = g.Max(x => x.Id) })
            .ToListAsync(cancellationToken);

        var laterCandidates = await _db.ItemMovementHistories
            .Where(x => ids.Contains(x.ItemInstanceId) && !(x.DocumentType == documentType && x.DocumentId == documentId))
            .Select(x => new { x.ItemInstanceId, x.Id })
            .ToListAsync(cancellationToken);

        return maxPerInstance
            .Where(item => laterCandidates.Any(x =>
                x.ItemInstanceId == item.ItemInstanceId &&
                (x.Id > item.LastId)))
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
                serialNumber = x.ItemInstance?.SerialNumber,
                newSerialNumber = (string?)null,
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

    private ServiceResult<DocumentMutationResultDto> Edited(string type, int id, string documentNo)
    {
        return ServiceResult<DocumentMutationResultDto>.Ok(new DocumentMutationResultDto
        {
            Action = "Edit",
            DocumentId = id,
            DocumentNo = documentNo,
            DocumentType = type,
            ProcessedAt = _clock.UtcNow
        }, "Document updated.");
    }

    private static void Touch(AuditableEntity entity, CurrentUserContext user, DateTime now)
    {
        entity.UpdatedAt = now;
        entity.UpdatedBy = user.UserName;
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string SerialKey(string itemCode, string? serialNumber)
        => $"{itemCode.Trim().ToUpperInvariant()}:{serialNumber?.Trim().ToUpperInvariant()}";

    private static bool IsInWarehouse(ItemStatus status)
        => status is ItemStatus.Normal or ItemStatus.InStock or ItemStatus.Damaged or ItemStatus.Scrapped;

    private static string LocationDisplayText(CurrentItemLocation location)
    {
        if (location.BinLocation != null) return location.BinLocation.FullPath;
        if (!string.IsNullOrWhiteSpace(location.ExternalLocationText))
        {
            return location.ExternalParty != null
                ? $"{location.ExternalParty.Name} - {location.ExternalLocationText}"
                : location.ExternalLocationText;
        }
        if (location.ExternalParty != null) return location.ExternalParty.Name;
        if (location.Warehouse != null) return location.Warehouse.Name;
        if (!string.IsNullOrWhiteSpace(location.ReferenceDocumentNo)) return $"{location.LocationType} / {location.ReferenceDocumentNo}";
        return location.LocationType.ToString();
    }

    private static bool LinesEquivalent(object currentPayload, JsonElement newPayload)
    {
        var currentNode = JsonSerializer.SerializeToNode(currentPayload, JsonOptions);
        var newNode = JsonNode.Parse(newPayload.GetRawText());
        var currentLines = GetCanonicalLines(currentNode);
        var newLines = GetCanonicalLines(newNode);
        return currentLines.SequenceEqual(newLines, StringComparer.Ordinal);
    }

    private static ItemStatus ResolveStatus(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return ItemStatus.Normal;
        if (Enum.TryParse<ItemStatus>(condition.Trim(), true, out var parsed))
        {
            // Legacy InStock maps to Normal
            return parsed == ItemStatus.InStock ? ItemStatus.Normal : parsed;
        }
        return ItemStatus.Normal;
    }
    private static IReadOnlyCollection<string> GetCanonicalLines(JsonNode? payload)
    {
        if (payload is not JsonObject obj || obj["lines"] is not JsonArray lines)
        {
            return Array.Empty<string>();
        }

        return lines
            .Select(CanonicalizeJson)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CanonicalizeJson(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var parts = obj
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{JsonSerializer.Serialize(x.Key)}:{CanonicalizeJson(x.Value)}");
            return "{" + string.Join(",", parts) + "}";
        }

        if (node is JsonArray array)
        {
            return "[" + string.Join(",", array.Select(CanonicalizeJson)) + "]";
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return JsonSerializer.Serialize(string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim());
        }

        return node?.ToJsonString(JsonOptions) ?? JsonSerializer.Serialize(string.Empty);
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
