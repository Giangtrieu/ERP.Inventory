# System Audit Summary

## Scope

Audit documents created under `docs/SystemAudit`:

- `InventoryBusinessLogic.md`
- `DocumentTypeRules.md`
- `LifecycleBatchAnalysis.md`
- `SuperPasswordAudit.md`
- `LocalizationAudit.md`
- `PerformanceAudit.md`
- `ImportAudit.md`
- `KnownIssues.md`
- `ProposedFixes.md`

No code was changed and no build was run.

## Current Logic Summary

- Location-tracked inventory is driven by item instances, current locations, movement history, inventory transactions, and stock balances.
- Quantity-only inventory uses separate quantity documents, lines, transactions, and balances.
- Borrow/Repair lifecycle rollback source of truth is `docs/TaskSummary/BorrowRepairLifecycleBatch.md`.
- Direct Borrow/Repair delete paths use latest `LifecycleBatchId` and are aligned with that source of truth.
- Inventory check is a session workflow with create, repeated scan batches, and finalize.
- Multi-use documents append batches under one unique document header.
- SuperPassword is implemented as a special login path using `AuthMode = Super`.
- Localization is catalog-based with frontend resource caching and page reload on language switch.
- Import supports canonical and localized parser aliases, but confirm paths are mixed between service calls and direct writes.

## Main Problems Found

- Selective edit for Borrow Return and Repair Receive can reverse too broadly because it does not filter by latest `LifecycleBatchId`.
- Inbound, BorrowLend, RepairSend, and InventoryCheck imports do not fully match manual workflows.
- Inbound import can miss lifecycle batch ids, stock balance updates, and actual status preservation.
- BorrowLend import can miss stock decrement and lifecycle log batch id.
- RepairSend import can miss repair lifecycle logs.
- SuperPassword has a hardcoded fallback, null-user crash risk, inconsistent claims, and incomplete business-permission bypass.
- Inventory check updates stock/history but not inventory transactions.
- Language switching reloads the page and loses unsaved form state.
- Import templates are not generated with localized headers.
- Production startup migration and row-by-row lookup patterns are performance risks.

## Root Cause Summary

The codebase has been standardized incrementally. Core manual services have newer lifecycle and rollback behavior, while imports, selective edit helpers, localization resources, and some security paths still contain older direct logic or partial integrations.

## Proposed Solution Summary

Recommended order:

1. Add scenario verification for repeated Borrow/Repair lifecycle batches.
2. Fix SuperPassword safety and claims.
3. Align selective edit rollback with latest `LifecycleBatchId`.
4. Refactor imports to use manual services or reproduce side effects exactly.
5. Add inventory check transaction rows and correct missing old status.
6. Complete localization coverage and preserve form drafts across language changes.
7. Move production migrations out of startup and optimize slow query/import paths.

## Data Migration Impact Summary

Potential backfill/rebuild work:

- Rebuild `StockBalance` from `CurrentItemLocation`.
- Backfill missing lifecycle ids for import-created rows where possible.
- Generate missing transaction/log rows only when safely reconstructable.
- Correct or flag inventory check histories with inaccurate old status.
- Review imported Borrow/Repair rows without lifecycle logs.

## Compatibility Risk Summary

- Import fixes may change document append behavior, timestamps, logs, and validation messages.
- Security fixes may affect users who rely on current SuperPassword behavior.
- Localized templates may affect Excel automation using canonical headers.
- New indexes and transaction rows may add write overhead.
- Inventory check behavior changes must preserve session semantics.
