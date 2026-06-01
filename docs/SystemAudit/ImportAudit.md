# Import Audit

## Current Logic

- Import workflow:
  1. Upload reads Excel via `SimpleExcel.ReadTableAsync`.
  2. Headers are canonicalized with `HeaderAliases`, including localized aliases from `ExcelResources`.
  3. Rows are stored in `ImportBatchRows` as JSON.
  4. Validate performs per-row and batch-level validation.
  5. Confirm revalidates and posts operational data.
- Supported import types:
  - ItemMaster
  - WarehouseStructure
  - Inbound
  - InventoryCheck
  - RepairSend
  - BorrowLend
  - QuantityInbound
  - QuantityOutbound
  - QuantityAdjust
  - MoveLocation
  - BorrowReturn
  - RepairReceive
- Some confirm paths call domain services:
  - Quantity operations call `IQuantityInventoryService`.
  - Move calls `IInventoryOperationService`.
  - BorrowReturn calls `IBorrowService.ReturnAsync`.
  - RepairReceive calls `IRepairService.ReceiveFromRepairAsync`.
- Other confirm paths write directly:
  - Inbound uses bulk insert and manual effect creation.
  - BorrowLend manually creates documents/effects.
  - RepairSend manually creates documents/effects.
  - InventoryCheck import creates finalized inventory check documents directly.

## Problems Found

### IMP-001 - Import templates are not generated with localized headers

`TemplateAsync` uses canonical `ImportHeaders` directly. The parser supports localized aliases, but exported templates do not reflect the current user's language.

### IMP-002 - Inbound import does not match manual inbound effects

Inbound import manually bulk-inserts data instead of calling `InboundService.CreateInboundAsync`.

Observed differences:

- No `LifecycleBatchId` is assigned to inbound logs, movement histories, or transactions.
- `StockBalance` is not rebuilt/incremented in the shown bulk insert path.
- Movement history and transaction status are forced to `Normal` even when inbound condition may differ.
- Source/receiver party handling differs from manual service.
- Append to existing inbound document by `DocumentNo` is not clearly aligned with manual append behavior.

### IMP-003 - BorrowLend import does not match manual borrow lend

Borrow lend import manually creates the document/effects instead of calling `BorrowServiceImpl.LendAsync`.

Observed differences:

- One `LifecycleBatchId` is created per row, not one per posted document/group operation.
- `BorrowDocumentLog` does not receive `LifecycleBatchId`.
- Stock balance decrement is missing in the shown path.
- Borrower lookup can leave `BorrowerId = 0`, which risks FK/data integrity failure.
- The code creates a new `BorrowDocument` for the group instead of reliably appending to an existing document.

### IMP-004 - RepairSend import does not match manual repair send

Repair send import manually creates effects instead of calling `RepairServiceImpl.SendToRepairAsync`.

Observed differences:

- One `LifecycleBatchId` is created per row, not one per posted operation.
- No `RepairDocumentLog` rows are created in the shown path.
- Transaction warehouse/bin values are built after current location mutation, which increases risk of inconsistent transaction location.
- Vendor lookup uses `FirstAsync` before the null-create branch, so missing vendors can throw before creation logic.

### IMP-005 - InventoryCheck import bypasses session lifecycle

Inventory check import creates finalized documents directly instead of using create session, scan batch, and finalize workflow. This can be acceptable for historical/import mode, but it is not equivalent to manual session behavior.

### IMP-006 - Some confirm paths use sync-over-async inside LINQ

BorrowReturn import uses `FindInstanceAsync(...).GetAwaiter().GetResult()` inside row projection. This can block threads and complicate error handling.

### IMP-007 - Import transaction boundaries are inconsistent

`RequiresOuterImportTransaction` includes only some import types. Other paths rely on called services or no outer transaction. Mixed behavior makes partial-confirm outcomes harder to reason about.

## Root Cause

- Import started as a direct data loader for speed and later business services evolved separately.
- Lifecycle batch logic was added after several import paths existed.
- Template localization and parser localization were implemented separately.

## Proposed Solution

- Use manual operation services for operational imports wherever feasible.
- For high-volume inbound, keep bulk insert only if it is made behaviorally identical:
  - assign one `LifecycleBatchId` per document/group batch,
  - write logs/history/transactions with the same batch id,
  - update/rebuild stock balances,
  - preserve actual status/condition,
  - append to existing documents correctly.
- Make BorrowLend and RepairSend imports delegate to `BorrowServiceImpl.LendAsync` and `RepairServiceImpl.SendToRepairAsync`, or clone their side effects exactly.
- Generate localized import templates with `Headers(user, ImportHeaders[importType])`.
- Keep parser aliases for canonical and localized headers.
- Remove sync-over-async from import row projections.
- Define one transaction policy per import type and document it.

## Data Migration Impact

- Existing imported inbound/borrow/repair data may have missing lifecycle batch ids, missing logs, missing stock balances, or incomplete transactions.
- A reconciliation/backfill script may be required:
  - backfill missing `LifecycleBatchId`,
  - rebuild stock balances from current locations,
  - generate missing transaction/log rows where reconstructable,
  - flag records that cannot be safely reconstructed.

## Compatibility Risks

- Switching imports to manual services may reduce throughput but improves correctness.
- Localized headers can affect external automation; aliases reduce risk.
- Backfilling historical imports may change reports that currently reflect incomplete data.
