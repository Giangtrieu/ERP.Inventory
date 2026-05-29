# Borrow / Repair Lifecycle Batch Identity

## Findings

- Borrow and Repair lifecycle logs already store status snapshots, but prior rollback grouped operations by timestamp and user.
- Repeated operations on the same document and item need a durable operation identity so the latest BorrowIssue, BorrowReturn, RepairSend, or RepairReceive can be deleted as one exact batch.
- `CurrentItemLocation` rebuild remains driven by existing movement history restoration.

## Root Cause

Rollback could not uniquely identify one posted lifecycle operation. Multiple rows created by one service call were related only by inferred timestamp/user grouping.

## Files Changed

- `src/ERP.Inventory.Domain/Entities/DocumentEntities.cs`
- `src/ERP.Inventory.Domain/Entities/TrackingEntities.cs`
- `src/ERP.Inventory.Domain/Entities/InventoryEntities.cs`
- `src/ERP.Inventory.Infrastructure/Data/InventoryDbContext.cs`
- `src/ERP.Inventory.Infrastructure/Services/InventoryOperationBase.cs`
- `src/ERP.Inventory.Infrastructure/Services/BorrowServiceImpl.cs`
- `src/ERP.Inventory.Infrastructure/Services/RepairServiceImpl.cs`
- `src/ERP.Inventory.Infrastructure/Services/DocumentLifecycleService.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/20260529083538_AddLifecycleBatchId.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/20260529083538_AddLifecycleBatchId.Designer.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/InventoryDbContextModelSnapshot.cs`

## Schema Changes

- Added nullable `LifecycleBatchId uniqueidentifier` to:
  - `BorrowDocumentLogs`
  - `RepairDocumentLogs`
  - `ItemMovementHistories`
  - `InventoryTransactions`
- Added lookup indexes for lifecycle rollback.
- `BorrowDocumentLogs.Action` and `RepairDocumentLogs.Action` are constrained to `nvarchar(450)` by EF so they can participate in indexes.

## Migration Steps

- Apply EF migration `20260529083538_AddLifecycleBatchId`.
- The migration backfills existing Borrow/Repair log rows with deterministic batch ids based on legacy grouping keys.
- It then backfills matching movement history and inventory transaction rows by per-item lifecycle sequence.
- Rollback still supports null batch ids by falling back to legacy latest-row behavior.

## Verification Steps

- `dotnet build ERP.Inventory.sln`
- Manually verify:
  - `Borrow -> Return -> Borrow -> Return -> Borrow`, deleting latest phase repeatedly.
  - `RepairSend -> RepairReceive -> RepairSend -> RepairReceive`, deleting latest phase repeatedly.
  - Existing inbound, move, adjustment, and location rebuild flows still rebuild from movement history.

## Remaining Risks

- Legacy data with truly ambiguous timestamp/user grouping cannot be perfectly reconstructed; the backfill mirrors the old grouping semantics and keeps runtime fallback.
- The solution compiles, but scenario-level database verification has not been run in this turn.
