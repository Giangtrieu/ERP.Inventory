# Document Type Rules Audit

## Current Logic

## Single-use Documents

Single-use documents are intended to have one immutable business document identity.

Observed single-use types:

- Move
- Adjustment
- Inventory Check session document number per period

Rules:

- `MoveDocument.DocumentNo` is unique in the database.
- `AdjustmentDocument.DocumentNo` is unique in the database.
- Editing uses selective mutation for affected lines.
- Delete removes all document effects and rebuilds location state from history.
- Rebuild reverses the document and replays the current edit payload.

## Multi-use Documents

Multi-use documents append operational batches under the same document number.

Observed multi-use types:

- Inbound
- Borrow Lend / Borrow Return
- Repair Send / Repair Receive
- Quantity Receive / Issue / Adjust

Rules:

- The database still has unique indexes on document number tables.
- Multi-use behavior is implemented by finding an existing document by `DocumentNo` and appending lines/effects rather than inserting a second document row.
- `LifecycleBatchId` identifies one posted batch inside the document.
- Delete should reverse the latest batch for the requested phase, not the entire document unless no effects remain.

## Problems Found

### DOC-001 - Multi-use semantics depend on service code, while database uniqueness still looks single-use

The unique indexes on `InboundDocument`, `BorrowDocument`, `RepairDocument`, and `QuantityInventoryDocument` are compatible with append behavior, but only if every creation path correctly finds and appends to the existing document.

### DOC-002 - Import paths do not always use the same services as manual create

Some imports call business services, but other imports manually create documents/effects. Those paths can bypass append behavior, lifecycle batch consistency, logs, or stock balance rules.

### DOC-003 - Edit behavior differs by document type

Some document types use selective mutation, quantity edit uses delete/repost, and rebuild uses broader reverse/replay. This is workable, but must be documented because it affects data recovery and user expectations.

## Root Cause

- Document number uniqueness is enforced at schema level.
- Multi-use append semantics were added in service logic after the schema existed.
- Import code contains older direct-write logic rather than delegating consistently to the same manual operation services.

## Proposed Solution

- Treat database unique document number as "one header per business document code".
- Keep append rules in one documented service layer per document family.
- Refactor imports to call the same manual services where possible.
- Add test scenarios for duplicate `DocumentNo` create/import/edit per document type.

## Data Migration Impact

No immediate schema migration is required. If duplicate headers already exist in production from older versions, a merge migration may be needed before enforcing append-only semantics.

## Compatibility Risks

- Tightening append behavior can alter import behavior for files that currently expect new headers.
- Consolidating import through manual services may change timestamps, audit messages, or validation errors.
