# Proposed Fixes

## Current Logic

This file consolidates proposed fixes from the audit documents. No code changes have been made as part of the audit.

## Problems Found

Key issue groups:

- Lifecycle batch mismatch in selective edit.
- Import paths that do not match manual operation side effects.
- SuperPassword security and authorization inconsistencies.
- Inventory check missing transaction rows and possible status inaccuracies.
- Localization gaps and form state loss on language switching.
- Performance risks from startup migration, row-by-row queries, and large payloads.

## Root Cause

The system has been standardized incrementally. Some newer rules, especially `LifecycleBatchId` and multi-use append behavior, are implemented in core services but not consistently applied to imports, selective edit helpers, and auxiliary workflows.

## Proposed Solution

Recommended implementation order:

1. Protect production data first:
   - Add scenario tests/manual scripts for repeated Borrow/Repair lifecycle rollback.
   - Back up production before migration/backfill.
2. Fix SuperPassword safety:
   - Remove hardcoded fallback.
   - Fix null user handling and claims.
   - Align super mode with warehouse permission checks.
3. Fix lifecycle edit rollback:
   - Filter Borrow Return and Repair Receive selective edit reversals by latest `LifecycleBatchId`.
4. Fix import correctness:
   - Delegate BorrowLend, RepairSend, BorrowReturn, RepairReceive, Move, and Quantity imports to business services.
   - Make inbound bulk import produce identical logs/history/transactions/stock/lifecycle data.
5. Fix inventory check transaction consistency:
   - Add transaction rows for wrong-location, extra, and missing/finalize effects.
   - Correct old status in missing history.
6. Improve localization:
   - Persist form drafts before language reload or switch language without reload.
   - Replace hardcoded JS/server strings with catalog keys.
   - Localize import template headers.
7. Performance hardening:
   - Move production migrations out of app startup.
   - Add slow-query monitoring and targeted indexes.
   - Batch preload import/manual operation lookups.

## Data Migration Impact

Likely migration/backfill work:

- Backfill missing import lifecycle batch ids where possible.
- Rebuild `StockBalance` from `CurrentItemLocation` for location-tracked items.
- Rebuild `QuantityStockBalance` from quantity transactions where needed.
- Optionally generate missing inventory check transactions from movement history.
- Flag imported Borrow/Repair rows lacking logs or lifecycle linkage for manual review.

## Compatibility Risks

- Correcting imports may change document counts, audit history, and reports.
- Security claim changes may affect authorization policies.
- New indexes and transaction rows can affect write performance.
- Language/template changes can affect users' existing Excel automation.
