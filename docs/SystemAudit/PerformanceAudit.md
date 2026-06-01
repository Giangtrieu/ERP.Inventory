# Performance Audit

## Current Logic

- The application uses EF Core with SQL Server-style indexes configured in `InventoryDbContext`.
- Important indexes exist for:
  - document numbers,
  - item serial/barcode,
  - current location by item instance,
  - stock balance uniqueness,
  - lifecycle batch lookup for movement history, transactions, and logs,
  - reconciliation sessions/results.
- Several read endpoints use `AsNoTracking`.
- Import inbound uses `EFCore.BulkExtensions` for large insert batches.
- Import batch list is capped to 50.
- Reconciliation result list uses paging.

## Problems Found

### PERF-001 - Startup runs database migration automatically

`Program.cs` calls `db.Database.MigrateAsync()` during application startup. On production-sized databases this can slow startup or introduce deployment risk.

### PERF-002 - Dashboard/report/list queries may scan large history tables

Movement history, inventory transactions, audit logs, and current locations are queried by filters such as status, document number, item code, and dates. Not all likely filter combinations have covering indexes.

### PERF-003 - Import validation and confirm contain per-row database calls

Several import paths validate or confirm rows with repeated `Find*` queries inside loops. Some paths preload dictionaries, but others still perform row-by-row lookups.

### PERF-004 - Borrow/Repair manual operations query item/current location per line

Borrow, repair, move, and return operations resolve instances and current locations inside loops. This is acceptable for small forms but can become slow for large line counts/import-like usage.

### PERF-005 - Localization resources are very large and served as one catalog

`LocalizationCatalog` is a large static dictionary. If the whole catalog is sent to the browser, every screen pays the payload cost even when it needs only part of it.

### PERF-006 - Rebuild/delete may repeatedly rebuild current locations and stock

Lifecycle operations rebuild affected item locations and recalculate stock balances. This is correct but can be expensive for broad documents or repeated delete/rebuild operations.

### PERF-007 - Some UI dropdowns may load large lookup sets

Lookup endpoints and page boot state can load warehouses, bins, items, parties, and users. Large master data should use server-side search/paging where possible.

## Root Cause

- The system favors correctness and direct EF queries over optimized batch pipelines.
- Import and manual services share some logic but not all preload strategies.
- Startup migration simplifies deployment but moves migration cost into app boot.
- Localization is implemented as a static all-in-one catalog.

## Proposed Solution

- Move production migrations to deployment pipeline; keep startup migration only for development or explicitly configured environments.
- Add query logging and capture slow SQL from production.
- Add or verify indexes for:
  - `ItemMovementHistories(DocumentNo)`, `PerformedAt`, and document/action/batch combinations.
  - `InventoryTransactions(DocumentNo)`, `PostedAt`, and item/status filters.
  - `CurrentItemLocations(WarehouseId, BinLocationId)`.
  - `AuditLogs(CreatedAt)`, `AuditLogs(ReferenceNo)`.
  - `ImportBatchRows(ImportBatchId, RowNumber)`.
- Batch preload item instances, current locations, bins, warehouses, and parties for multi-line operations/imports.
- Split localization resources by module or add ETag/versioned caching.
- Ensure lookup endpoints support keyword search and pagination for large tables.

## Data Migration Impact

Adding indexes requires database migrations and should be tested against production data volume. Index creation may need online scheduling.

## Compatibility Risks

- New indexes increase write overhead.
- Removing startup migration requires reliable deployment process.
- Lookup paging can require frontend changes where screens currently expect full lists.
