# Known Issues

## Current Logic

Borrow and Repair issues in this file must be validated against `docs/TaskSummary/BorrowRepairLifecycleBatch.md` before being recorded.

Do not mark a Borrow/Repair behavior as a business bug when it matches the lifecycle batch source of truth. Only record an issue when actual behavior differs from that document or can produce data inconsistency.

## Problems Found

### BR-LC-001 - Selective edit can remove older Borrow/Repair lifecycle effects

Severity: High.

The direct delete paths for Borrow and Repair use `LifecycleBatchId` correctly. The selective edit paths for Borrow Return and Repair Receive still reverse effects by affected `ItemInstanceId` and phase type, without limiting removal to the latest lifecycle batch.

Impact:

- Repeated Borrow/Repair lifecycle history can be partially deleted during edit.
- Movement history can lose older return/receive phases.
- `CurrentItemLocation` rebuild can replay from incomplete history.
- Inventory transactions for older phases can be removed from audit trail.

## Root Cause

`ReverseReturnPhaseAsync`, `ReverseRepairReceivePhaseAsync`, and `ReverseLocationTrackedPhaseAsync` do not receive or apply `LifecycleBatchId`.

## Proposed Solution

Align selective edit reverse behavior with `DeleteBorrowReturnAsync` and `DeleteRepairReceiveAsync`:

- Resolve the latest phase log.
- Reverse only the matching `LifecycleBatchId`.
- Keep legacy null-batch fallback only for old data.

## Data Migration Impact

No new migration expected. Existing null batch rows need the documented legacy fallback.

## Compatibility Risk

Medium. The change is behaviorally stricter and prevents old batch effects from being removed during edit.

## Recommended Implementation Order

1. Add repeated lifecycle verification cases.
2. Patch selective edit reverse helpers to filter by lifecycle batch.
3. Verify state rebuild and stock balance recalculation.

### INV-001 - Inventory check lacks inventory transaction rows

Inventory check wrong-location, extra, and missing/finalize effects update stock and movement history but do not create `InventoryTransaction` rows.

Impact: transaction-based reports can differ from stock/current-location state.

### INV-002 - Inventory check missing history can record incorrect old status

Finalize writes missing history with `OldStatus = InStock` instead of the item instance's actual old status.

Impact: movement audit trail can misrepresent the previous item state.

### IMP-001 - Import templates are not localized

`TemplateAsync` emits canonical headers from `ImportHeaders` instead of localized headers for the current user.

Impact: generated templates do not meet the multilingual import requirement.

### IMP-002 - Inbound import differs from manual inbound

Inbound import uses manual bulk insert logic. It can miss `LifecycleBatchId`, stock balance updates, and exact status/condition behavior.

Impact: imported inbound data may not rebuild/delete like manual data.

### IMP-003 - BorrowLend import differs from manual borrow lend

Borrow lend import writes effects directly, creates lifecycle batch ids per row, omits batch id on borrow logs, and does not clearly decrement stock in the shown path.

Impact: imported borrow data can break lifecycle rollback and stock consistency.

### IMP-004 - RepairSend import differs from manual repair send

Repair send import writes effects directly and does not create `RepairDocumentLog` rows in the shown path.

Impact: imported repair send records may not participate in lifecycle rollback as documented.

### IMP-005 - InventoryCheck import bypasses session lifecycle

Inventory check import creates finalized check documents directly instead of using create session, scan batch, and finalize.

Impact: import behavior is not equivalent to manual workflow and needs to be explicitly treated as historical/bulk mode or refactored.

### SP-001 - SuperPassword has unsafe fallback and inconsistent claims

The super password has a hardcoded fallback, can crash when username is not found, does not truly bypass username lookup, emits duplicate display claims, and may not satisfy the `SuperOnly` policy.

Impact: emergency access can be unreliable and creates security risk.

### LOC-001 - Language switch loses unsaved form state

Language switching rewrites the auth cookie and reloads the page without saving active form drafts.

Impact: users lose unsaved operation/import/edit data.

### LOC-002 - Hardcoded UI/server strings remain

Several JavaScript confirmations/toasts and service errors still use literal strings.

Impact: Vietnamese/English/Chinese coverage is incomplete.

### PERF-001 - Production startup migration risk

The app runs `Database.MigrateAsync()` during startup.

Impact: production startup can be slow or risky during schema changes.

### PERF-002 - Row-by-row import/service lookups can be slow

Some import and multi-line operation paths perform database lookups inside loops.

Impact: large imports or documents can become slow and increase database load.
