# Inventory Business Logic Audit

## Current Logic

### Shared inventory model

- Location-tracked assets use `ItemInstance`, `CurrentItemLocation`, `ItemMovementHistory`, `InventoryTransaction`, and `StockBalance`.
- Quantity-only inventory uses `QuantityInventoryDocument`, `QuantityInventoryDocumentLine`, `QuantityInventoryTransaction`, and `QuantityStockBalance`.
- `DocumentBase.Status` is usually `Posted`; inventory check uses a separate `SessionStatus`.
- `InventoryStatePolicy` defines allowed transitions:
  - Move: `Normal`, legacy `InStock`, `Damaged`, `Scrapped`.
  - Borrow lend: `Normal`, legacy `InStock`.
  - Repair send: `Normal`, legacy `InStock`, `Damaged`.
  - Borrow return maps condition to `Normal`, `Damaged`, `Lost`, or `Scrapped`.
  - Repair receive maps success/replaced to `Normal`, failed to `Damaged`.

### Inbound

- Manual inbound creates or appends to an existing `InboundDocument` by `DocumentNo`.
- Each posted line creates an `ItemInstance`, `InboundDocumentLine`, `CurrentItemLocation`, `InboundDocumentLog`, `ItemMovementHistory`, `InventoryTransaction`, and stock balance increment.
- Recent code uses `LifecycleBatchId` for inbound logs, movement history, and transactions.
- Delete reverses the latest inbound batch and removes matching item instances/current locations.

### Move Location

- Move is a single-use document.
- Manual move validates source item state, selected warehouse, target bin ownership, and target occupancy.
- It updates `CurrentItemLocation`, creates `MoveDocumentLine`, movement history, inventory transaction, and recalculates stock deltas.
- Delete removes move effects and rebuilds item location from movement history.

### Adjustment

- Adjustment is a single-use document.
- Normal adjustment changes status and/or location for an existing item instance.
- Replacement adjustment marks the old instance and creates a new replacement instance.
- Effects include adjustment lines, adjustment logs, movement history, inventory transaction, current location updates, and stock balance deltas.
- Delete removes adjustment effects and rebuilds location-tracked instances.

### Inventory Check

- Inventory check is session-based and must not be treated like a normal one-shot document.
- `CreateSessionAsync` creates or reuses a period-based `InventoryCheckDocument` with `SessionStatus = InProgress`.
- `ScanBatchAsync` can be called multiple times before finalize.
- Scan results:
  - `Matched`: records a line, may normalize non-warehouse status back to `Normal`.
  - `WrongLocation`: records a line, immediately moves the item to the scanned bin, adjusts stock, and adds movement history.
  - `Extra`: if item type exists but serial is unknown, creates a new item instance/current location and increments stock.
  - Unknown item type is recorded as extra without creating an item instance.
- `FinalizeAsync` marks unscanned warehouse items as `Missing`; normal/in-stock missing items are set to `Lost`, removed from bin stock, and movement history is added.
- Finalize sets `SessionStatus = Finalized`, adds audit/notification side effects, and cannot be repeated.

### Quantity Inventory

- Quantity documents use multi-use `DocumentNo` semantics with `LifecycleBatchId`.
- Receive increases `QuantityStockBalance`.
- Issue decreases quantity and validates availability.
- Adjust sets or reconciles the target quantity depending on operation path.
- Delete reverses the latest quantity batch using `LifecycleBatchId`, removes matching document lines, and cleans orphan quantity-only item instances.

### Borrow / Return

- Borrow documents are multi-use by `DocumentNo`.
- Lend changes status to `LentOut`, moves item to borrower/external location, decrements bin stock, and writes log/history/transaction with `LifecycleBatchId`.
- Return changes status based on return condition, moves item to bin or unknown/lost, updates borrow line return state, adjusts stock, and writes log/history/transaction with `LifecycleBatchId`.
- Direct delete paths are aligned with `docs/TaskSummary/BorrowRepairLifecycleBatch.md`: latest lifecycle batch only.

### Repair Send / Receive

- Repair documents are multi-use by `DocumentNo`.
- Send changes status to `Repairing`, moves item to repair vendor/external location, decrements bin stock, and writes log/history/transaction with `LifecycleBatchId`.
- Receive changes status based on repair result, moves item to target bin, updates repair line return state, increments stock, and writes log/history/transaction with `LifecycleBatchId`.
- Direct delete paths are aligned with `docs/TaskSummary/BorrowRepairLifecycleBatch.md`: latest lifecycle batch only.

## Problems Found

### INV-001 - Inventory check creates movement history but not inventory transactions

Wrong-location, extra, and missing/finalize operations update stock and movement history, but no `InventoryTransaction` rows are created for the inventory check effects.

### INV-002 - Inventory check duplicate validation is bin-based, not item-based

`ScanBatchAsync` rejects another line if the same `ActualBinLocationId` already exists in the document. This assumes one active item per bin. If any warehouse area allows multiple serials per bin, inventory check cannot scan them.

### INV-003 - Missing finalize history records old status as `InStock`

Finalize records missing item history with `OldStatus = ItemStatus.InStock` even when the actual old status is `Normal`, `Damaged`, or `Scrapped`. The item is only changed to `Lost` for `Normal` or `InStock`.

### INV-004 - Adjustment and move are full-document delete/rebuild for delete paths

Move and adjustment delete remove all effects for the document and rebuild from history. This is acceptable for single-use documents but should remain documented because it differs from batch-based multi-use documents.

## Root Cause

- Inventory check was implemented as a session correction workflow, not a standard inventory transaction workflow.
- Some scan/finalize logic updates stock directly and writes movement history, but does not mirror the transaction table contract used by other modules.
- The code assumes physical bin occupancy is unique for location-tracked assets.

## Proposed Solution

- Add explicit `InventoryTransactionType.InventoryCheck` or equivalent transaction rows for wrong-location, extra, and missing effects.
- Use the actual old status when writing missing movement history.
- Confirm whether one serial per bin is a hard business rule. If not, replace bin-level duplicate checks with item/serial-level duplicate checks.
- Keep inventory check session lifecycle separate from document edit/delete flows.

## Data Migration Impact

- Existing inventory check sessions may lack transaction rows. If transaction history is used for reporting, a backfill should generate transactions from inventory check movement history.
- Existing missing movement history may show incorrect old status and may need correction from prior movement/current state snapshots if accuracy is required.

## Compatibility Risks

- Adding inventory check transactions may change reports that currently rely only on movement history or stock balance.
- Relaxing bin duplicate checks could conflict with current one-item-per-bin UI assumptions.
