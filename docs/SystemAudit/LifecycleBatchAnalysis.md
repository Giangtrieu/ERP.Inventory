# Lifecycle Batch Analysis

## Current Logic

`docs/TaskSummary/BorrowRepairLifecycleBatch.md` is the source of truth for Borrow and Repair rollback behavior.

Official behavior:

- Borrow and Repair lifecycle operations are grouped by `LifecycleBatchId`.
- One posted operation can contain multiple rows, but all rows from that operation must share the same `LifecycleBatchId`.
- Delete/rollback must reverse the latest lifecycle operation batch only.
- Movement history and inventory transactions must be removed consistently with the same lifecycle batch.
- `CurrentItemLocation` is rebuilt from movement history after rollback.
- Legacy rows with null `LifecycleBatchId` are supported only as fallback behavior.

Observed implementation:

- `BorrowServiceImpl.LendAsync` creates one `lifecycleBatchId` per post and writes it to `BorrowDocumentLog`, `ItemMovementHistory`, and `InventoryTransaction`.
- `BorrowServiceImpl.ReturnAsync` creates one `lifecycleBatchId` per return post and writes it to the same three effect tables.
- `RepairServiceImpl.SendToRepairAsync` creates one `lifecycleBatchId` per send post and writes it to `RepairDocumentLog`, `ItemMovementHistory`, and `InventoryTransaction`.
- `RepairServiceImpl.ReceiveFromRepairAsync` creates one `lifecycleBatchId` per receive post and writes it to the same three effect tables.
- `DocumentLifecycleService.DeleteBorrowLendAsync`, `DeleteBorrowReturnAsync`, `DeleteRepairSendAsync`, and `DeleteRepairReceiveAsync` choose the latest lifecycle log, read its `LifecycleBatchId`, and remove matching logs/movements/transactions for that latest batch.

The direct delete paths are aligned with the lifecycle source of truth at code level.

## Problems Found

### BR-LC-001 - Selective edit rollback ignores lifecycle batch for Borrow Return and Repair Receive

Status: confirmed issue.

Affected code:

- `DocumentLifecycleService.EditBorrowReturnSelectiveAsync`
- `DocumentLifecycleService.EditRepairReceiveSelectiveAsync`
- `DocumentLifecycleService.ReverseReturnPhaseAsync`
- `DocumentLifecycleService.ReverseRepairReceivePhaseAsync`
- `DocumentLifecycleService.ReverseLocationTrackedPhaseAsync`

The edit path computes affected item instances, then removes all matching return/receive logs, movement histories, and inventory transactions for those item instances in the document phase. It does not limit removal to the latest `LifecycleBatchId`.

This differs from the lifecycle source of truth because rollback/edit should reverse one exact operation batch, not every historical return/receive phase for the same item instance.

## Root Cause

Delete paths were updated to use `LifecycleBatchId`, but selective edit helpers still use broad filters:

- Borrow return logs are removed by `BorrowDocumentId`, `Action == "BorrowReturn"`, and affected `ItemInstanceId`.
- Repair receive logs are removed by `RepairDocumentId`, `Action == "RepairReceive"`, and affected `ItemInstanceId`.
- Movement histories and inventory transactions are removed by document, phase action/type, and affected `ItemInstanceId`.

These filters are unsafe for repeated lifecycle scenarios:

- `Borrow -> Return -> Borrow -> Return`
- `RepairSend -> RepairReceive -> RepairSend -> RepairReceive`

## Proposed Solution

Selective edit for Borrow Return and Repair Receive should identify the latest lifecycle batch for the affected phase before reversing effects.

Recommended behavior:

- Find the latest `BorrowDocumentLog` or `RepairDocumentLog` for the edited phase.
- Require the latest action to match the phase being edited.
- Capture `latestLog.LifecycleBatchId`.
- Reverse only logs, movement histories, inventory transactions, and line state derived from that exact batch.
- For null batch ids, keep the documented legacy fallback grouping by timestamp/user.
- Rebuild `CurrentItemLocation` and stock balances only for item instances affected by the reversed batch.

## Data Migration Impact

No schema migration is required for new data because `LifecycleBatchId` already exists.

Existing data remains dependent on the migration/backfill described in `BorrowRepairLifecycleBatch.md`. If old rows still have null `LifecycleBatchId`, selective edit must keep the same legacy fallback semantics as delete.

## Compatibility Risk

Medium.

The fix changes edit behavior for repeated Borrow/Repair lifecycles. This is intentional because current selective edit can remove historical rows from older lifecycle batches and produce incomplete movement history.

## Recommended Implementation Order

1. Add focused tests or manual scenario scripts for repeated Borrow and Repair lifecycles.
2. Refactor selective edit reverse helpers to accept a lifecycle batch id or legacy grouping key.
3. Apply the same batch filtering used by delete paths.
4. Verify delete and edit for repeated lifecycle sequences.
5. Rebuild affected item state from movement history and compare stock balances.
