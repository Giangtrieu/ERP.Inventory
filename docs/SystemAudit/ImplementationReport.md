# Implementation Report

## BR-LC-001 - Selective edit can remove older Borrow/Repair lifecycle effects

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/DocumentLifecycleService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Summary

Selective edit rollback for Borrow Return and Repair Receive now resolves the latest lifecycle log and reverses only rows in that latest `LifecycleBatchId`.

The change preserves the existing `LifecycleBatchId` model and reuses the same movement history and inventory transaction removal helpers used by direct delete paths, including the documented null-batch legacy fallback.

The edit now rejects attempts to selectively reverse returned/received lines that are not part of the latest return/receive lifecycle batch. After reversal, document lines are replayed from remaining lifecycle logs before the edited payload is posted again.

### Migration Impact

No database schema migration is required.

Existing production rows with null `LifecycleBatchId` remain supported through the existing legacy fallback behavior in `RemoveLatestMovementHistoriesAsync` and `RemoveLatestInventoryTransactionsAsync`.

### Regression Risk

Medium.

The behavior is intentionally stricter for repeated Borrow/Repair lifecycle histories. Selective edits can no longer remove older return/receive effects when a later lifecycle batch exists. Existing valid latest-batch edit behavior is preserved.

### Validation Performed

- Confirmed root cause in `DocumentLifecycleService` source code.
- Verified direct delete paths already use latest `LifecycleBatchId` semantics.
- Updated selective edit helpers to filter logs, movement history, and inventory transactions by latest lifecycle batch.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.
- Ran `dotnet test ERP.Inventory.sln --no-build`.

## IMP-002 - Inbound import differs from manual inbound

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/ImportExportService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Summary

Manual inbound creation is the source of truth. It creates or appends the inbound document, document lines, item instances, current locations, stock balance deltas, movement history, inventory transactions, inbound logs, lifecycle batch ids, and post side effects through `InboundService.CreateInboundAsync`.

Inbound import previously wrote those tables directly with a bulk path. That path did not assign `LifecycleBatchId` to inbound logs, movement history, or inventory transactions; did not update `StockBalance`; forced history and transaction status to `Normal`; handled source/receiver party data differently; and did not use the same append/delete/rebuild contract as manual inbound.

Inbound import now groups rows by `DocumentNo`, builds `InboundRequest` payloads from the import rows, and posts them through `IInboundService.CreateInboundAsync`. This makes import use the same document, line, lifecycle batch, movement history, inventory transaction, stock balance, current location, log, post side effect, rebuild, and delete behavior as manual inbound creation.

### Migration Impact

No schema migration is required.

Existing imported inbound data may already have missing lifecycle ids, missing stock balances, or status mismatches. That historical data requires a separate reconciliation/backfill plan if production reports or delete/rebuild behavior depend on those rows. This implementation only corrects new inbound imports.

### Regression Risk

Medium.

Inbound imports now use manual service validation and side effects, so import behavior may reject rows that the older direct bulk path accepted. Throughput may be lower than the bulk insert path, but correctness now matches manual creation.

### Validation Performed

- Compared manual inbound side effects in `InboundService.CreateInboundAsync` with the previous direct import path.
- Confirmed import now calls the manual inbound service instead of writing operational effects directly.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## IMP-003 - BorrowLend import differs from manual borrow lend

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/ImportExportService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

Manual Borrow lend posts through `BorrowServiceImpl.LendAsync`, which creates/appends the borrow document, creates borrow lines, updates current location, decrements stock balance, writes borrow logs, movement history, inventory transactions, post side effects, and uses one `LifecycleBatchId` for the whole posted operation.

BorrowLend import was still writing borrow documents and effects directly. It generated lifecycle ids per row, omitted `LifecycleBatchId` on `BorrowDocumentLog`, missed stock balance decrement, could write `BorrowerId = 0`, and did not reliably append to an existing `DocumentNo`.

### Summary

BorrowLend import now groups rows by `DocumentNo`, builds a `BorrowLendRequest`, and delegates posting to `_borrowService.LendAsync`.

This preserves the existing Borrow lifecycle model and makes import side effects match manual Borrow creation for document creation/append, lines, lifecycle batch, logs, movement history, inventory transactions, current location updates, stock balance updates, delete behavior, rebuild behavior, and rollback behavior.

### Migration Impact

No schema migration is required.

Existing BorrowLend import data may already have missing log lifecycle ids, missing stock balance decrements, invalid borrower references, or per-row batch grouping. Historical correction requires a separate reconciliation/backfill plan if production data needs repair. This change corrects new BorrowLend imports only.

### Regression Risk

Medium.

BorrowLend import now uses manual service validation and side effects. Files accepted by the old direct-write path may now fail if they do not satisfy manual Borrow requirements, but that is required for import/manual consistency.

### Validation Result

- Compared manual Borrow lend service behavior with the previous BorrowLend import direct-write path.
- Confirmed new BorrowLend import path calls `_borrowService.LendAsync`.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## IMP-004 - RepairSend import differs from manual repair send

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/ImportExportService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

Manual RepairSend posts through `RepairServiceImpl.SendToRepairAsync`, which creates or appends the repair document, creates repair lines, updates current location, decrements stock balance, writes repair logs, movement history, inventory transactions, post side effects, and uses one `LifecycleBatchId` for the whole posted operation.

RepairSend import was still writing repair documents and effects directly. It generated lifecycle ids per row, did not create `RepairDocumentLog` rows, could throw before vendor creation because it used `FirstAsync`, and did not use the manual append/post-side-effect contract.

### Summary

RepairSend import now groups rows by `DocumentNo`, builds a `RepairSendRequest`, and delegates posting to `_repairService.SendToRepairAsync`.

This preserves the existing Repair lifecycle model and makes import side effects match manual RepairSend creation for document creation/append, lines, lifecycle batch, logs, movement history, inventory transactions, current location updates, stock balance updates, delete behavior, rebuild behavior, and rollback behavior.

### Migration Impact

No schema migration is required.

Existing RepairSend import data may already have missing repair logs, incomplete lifecycle linkage, or inconsistent operation grouping. Historical correction requires a separate reconciliation/backfill plan if production data needs repair. This change corrects new RepairSend imports only.

### Regression Risk

Medium.

RepairSend import now uses manual service validation and side effects. Files accepted by the old direct-write path may now fail if they do not satisfy manual RepairSend requirements, but that is required for import/manual consistency.

### Validation Result

- Compared manual RepairSend service behavior with the previous RepairSend import direct-write path.
- Confirmed new RepairSend import path calls `_repairService.SendToRepairAsync`.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## IMP-005 - InventoryCheck import bypasses session lifecycle

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/ImportExportService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

Manual Inventory Check is session-based and posts through `InventoryCheckService.CreateSessionAsync`, `ScanBatchAsync`, and `FinalizeAsync`.

InventoryCheck import created `InventoryCheckDocument` rows directly with `SessionStatus = "Finalized"` and manually wrote selected line, location, and stock effects. It did not delegate to the manual session workflow. The source also created a local `documents` list but never added the newly created documents to it, so the intended missing-item/finalize pass was not executed.

### Summary

InventoryCheck import now delegates to the manual Inventory Check workflow:

- creates or reuses the manual period-based check session,
- scans imported rows through `InventoryCheckService.ScanBatchAsync`,
- finalizes through `InventoryCheckService.FinalizeAsync`,
- relies on the manual service for reconciliation lines, current location updates, stock balance updates, movement history, finalize logs, notifications, and status transitions.

InventoryCheck import was removed from the outer import transaction list because the manual scan and finalize services own their transaction boundaries.

Validation now requires the fields required by manual scan and blocks multi-warehouse InventoryCheck files so one import maps to one manual check session.

### Migration Impact

No schema migration is required.

Existing InventoryCheck imports may already be marked finalized without missing lines, finalize audit/notification side effects, or movement history for missing items. Historical repair requires a separate reconciliation/backfill plan if production reporting or audit history depends on those rows. This implementation corrects new InventoryCheck imports only.

### Regression Risk

Medium.

InventoryCheck imports now use manual session validation and manual period-based document behavior. Files previously accepted by the direct loader may now fail if they omit manual-required fields or target a session that manual workflow would reject, such as a finalized period session.

### Validation Result

- Compared manual Inventory Check session behavior with the previous direct import path.
- Confirmed new InventoryCheck import path calls `CreateSessionAsync`, `ScanBatchAsync`, and `FinalizeAsync`.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## SP-001 - Hardcoded fallback super password exists

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/LogErrorSystemService.cs`
- `src/ERP.Inventory.Web/Program.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

`SuperAdminSecurity.SuperAdminPassword` was initialized with a hardcoded in-code value, and `Program.cs` kept that value when configuration key `SuperAdminPassword` was absent.

### Security Impact

If production configuration omitted `SuperAdminPassword`, emergency access silently remained enabled with a known static password.

### Migration / Configuration Impact

No database migration is required.

Deployments that require SuperPassword emergency access must now explicitly configure `SuperAdminPassword` in protected configuration or secret storage. If the setting is missing or blank, SuperPassword verification is disabled.

### Regression Risk

Medium.

This intentionally changes missing-configuration behavior. Normal username/password login is unaffected.

### Validation Result

- Verified the hardcoded fallback in source.
- Removed the fallback and configured an empty default when `SuperAdminPassword` is absent.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## SP-002 - Super login can crash when username does not match an active user

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Web/Controllers/AccountController.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

The login action loaded `SystemUser` by submitted username, then the super-password branch dereferenced `user.Id`, `user.UserName`, `user.DisplayName`, and `user.PreferredLanguage` before checking whether `user` was null.

### Security Impact

Emergency login with the correct SuperPassword and an unknown username could produce a server error instead of a controlled authentication result. This makes emergency access unreliable and can expose an avoidable error path.

### Migration / Configuration Impact

No database migration or data backfill is required.

### Regression Risk

Low.

The fix only changes the SuperPassword branch. Normal login still validates the submitted username and password through the existing user lookup.

### Validation Result

- Verified the null dereference in source.
- Moved SuperPassword verification before normal user lookup and stopped dereferencing `SystemUser` in super mode.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## SP-003 - Super password does not truly bypass username lookup

Date: 2026-05-31

### Files Changed

- `src/ERP.Inventory.Web/Controllers/AccountController.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Root Cause

SuperPassword login was embedded after the normal username lookup and used claims from the selected active user. As a result, emergency access still depended on a valid active username.

### Security Impact

The emergency-access path could fail during account lookup problems, inactive/missing users, or operator uncertainty about a valid username, undermining the purpose of SuperPassword.

### Migration / Configuration Impact

No database migration is required.

Super login now emits a dedicated emergency principal with `NameIdentifier = SuperAdmin`, `Name = SuperAdmin`, `display_name = SuperAdmin`, role `SystemSuperAdmin`, and `AuthMode = Super`. Audit logging action/result are preserved.

### Regression Risk

Medium.

Super sessions no longer inherit a real user's identity, language, roles, or warehouse claims from the submitted username. This is required for username-independent emergency access and aligns with the existing `AuthMode = Super` concept. Normal user login is unchanged.

### Validation Result

- Verified the username dependency in source.
- Updated SuperPassword login to create the super principal before normal `SystemUser` lookup and before username model validation.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

### Compatibility Review - 2026-05-31

Reviewed the new Super principal:

- `NameIdentifier = SuperAdmin`
- `Name = SuperAdmin`
- `display_name = SuperAdmin`
- role `SystemSuperAdmin`
- `AuthMode = Super`

#### Compatible Areas

- `CurrentUserService` reads `ClaimTypes.NameIdentifier` as a string and does not parse it as an integer.
- `CurrentUserContext.UserId` is a string, and `IsSuper`, `IsAdmin`, `CanManage`, `CanOperate`, and `CanAccessWarehouse` already treat `AuthMode = Super` as privileged.
- Warehouse permission checks in inventory, tracking, reconciliation, quantity inventory, import/export, lookup, and management paths rely on `CurrentUserContext.IsAdmin` or `CanAccessWarehouse`, so `AuthMode = Super` bypasses warehouse restrictions as intended.
- Audit logging stores `UserId` and `UserName` as strings. `SuperAdmin` fits existing `AuditLog`, `LogErrorSystem`, document audit, and movement/transaction operator fields.
- `CreatedBy`, `UpdatedBy`, `PerformedBy`, `PostedBy`, `ApprovedBy`, `ResolvedBy`, and quantity `OperatorUserId`/`OperatorUserCode` are string fields. No schema FK to `SystemUser` was found for these operational audit fields.
- No source code path was found that parses `ClaimTypes.NameIdentifier` as an integer.

#### Incompatibilities / Failure Scenarios

1. `AppController.SetLanguage` still treats `ClaimTypes.NameIdentifier` as a `SystemUser.Id`.
   - With the new Super principal, it queries `SystemUsers` for `Id == "SuperAdmin"`.
   - If no real `SystemUser` row has that id, the endpoint returns `404 NotFound`.
   - Affected API: `POST /App/Language`.
   - Business impact: Super sessions cannot persist language preference through the existing endpoint. The current cookie already contains the selected login language, so this is a UI/session-preference failure, not an inventory data failure.

2. Notification rows can be created with `UserId = "SuperAdmin"`.
   - `AppController.Bootstrap`, `NotificationsController.Unread`, `InventoryOperationBase.AddPostSideEffects`, and `InventoryCheckService.AddFinalizeNotification` use `UserId` from the principal.
   - `Notification.UserId` is a string and no FK to `SystemUser` is configured, so this does not break database writes.
   - Side effect: Super emergency sessions can accumulate notifications under the synthetic `SuperAdmin` id.

3. User-management self-protection no longer applies to a real admin user during Super sessions.
   - `UserManagementController` compares edited/deleted user ids to `CurrentUserContext.UserId`.
   - With `UserId = SuperAdmin`, the guard does not match any normal admin account.
   - This does not crash, but it means a Super session can deactivate or delete real admin users. That may be acceptable for emergency access, but it is broader than normal self-protection behavior.

#### Smallest Safe Fix Proposal

Do not change the Super principal back to a real user id, because that would reintroduce SP-003's username dependency.

Smallest safe code fix:

- Update `AppController.SetLanguage` to handle `AuthMode = Super` without requiring a `SystemUser` row:
  - normalize the requested language,
  - rebuild the current cookie claims with the new `language` claim,
  - return success,
  - skip `SystemUsers.PreferredLanguage` persistence for Super sessions.

Optional follow-up hardening:

- Suppress or specially scope notifications for `AuthMode = Super` if persistent notifications under synthetic id `SuperAdmin` are not desired.
- Decide whether Super sessions should be allowed to modify/delete real admin users or whether user-management destructive operations should add an explicit `user.IsSuper` guard.

No code changes were made during this review.

### Compatibility Fix Implemented - 2026-05-31

Files changed:

- `src/ERP.Inventory.Web/Controllers/AppController.cs`
- `docs/SystemAudit/ImplementationReport.md`

`AppController.SetLanguage` now checks `AuthMode = Super`. For normal users, it preserves the existing behavior: lookup `SystemUsers` by `NameIdentifier`, persist `PreferredLanguage`, and rebuild the auth cookie. For Super sessions, it skips the `SystemUsers` lookup and persistence, then rebuilds the auth cookie with the new `language` claim.

This keeps the synthetic Super principal design and does not reintroduce username dependency.

Migration / configuration impact: none.

Regression risk: low. The change is scoped to language switching and preserves the normal-user path.

## SuperPassword Remaining Issues Review - SP-004 to SP-006

Date: 2026-05-31

### Scope

Reviewed current code after SP-001, SP-002, SP-003, and the SP-003 language-switching compatibility fix.

Files reviewed:

- `src/ERP.Inventory.Web/Program.cs`
- `src/ERP.Inventory.Web/Controllers/AccountController.cs`
- `src/ERP.Inventory.Web/Middleware/SuperAdminAuthorizationHandler.cs`
- `src/ERP.Inventory.Web/Services/CurrentUserService.cs`
- `src/ERP.Inventory.Application/Common/CurrentUserContext.cs`
- warehouse-scoped service/controller usages of `IsAdmin`, `CanAccessWarehouse`, and `WarehouseIds`

No code changes were made for this review.

### SP-004 - `SuperOnly` policy appears inconsistent with super claims

Status: no longer reproducible after SP-003.

Current `SuperOnly` policy still requires:

- authenticated user,
- `AuthMode = Super`,
- role `SystemSuperAdmin`,
- `ClaimTypes.Name = SuperAdmin`.

Current Super login now emits:

- `ClaimTypes.NameIdentifier = SuperAdmin`,
- `ClaimTypes.Name = SuperAdmin`,
- `ClaimTypes.Role = SystemSuperAdmin`,
- `AuthMode = Super`.

The earlier mismatch came from setting `ClaimTypes.Name` to the selected user's username. That no longer happens. The custom role authorization handler still only handles role requirements, but the non-role claim requirements now match the emitted Super principal.

Proposed fix: none required for SP-004.

### SP-005 - Duplicate `display_name` claims are emitted

Status: no longer reproducible after SP-003.

Current Super login emits exactly one `display_name` claim:

- `display_name = SuperAdmin`.

The earlier duplicate came from adding both the selected user's display name and `SuperAdmin`. The Super branch no longer depends on a selected `SystemUser`, so it no longer emits the selected user's display name.

Proposed fix: none required for SP-005.

### SP-006 - Warehouse permissions are empty in super mode

Status: no longer reproducible as a business-service authorization failure.

Current Super login still does not emit `warehouse_id` claims, so `CurrentUserService.WarehouseIds` is empty for Super sessions. However, `CurrentUserContext` already treats `AuthMode = Super` as privileged:

- `IsSuper` is true when `AuthMode = Super`.
- `IsAdmin` returns true for `IsSuper`.
- `CanAccessWarehouse(...)` returns true for `IsSuper`.

Verified service/controller patterns:

- Direct write/operation checks use `user.CanAccessWarehouse(...)`; Super passes these checks.
- Read-scope filters commonly use `if (!user.IsAdmin)` or `user.IsAdmin ? query : scopedQuery`; Super is treated as admin and avoids empty-warehouse filtering.
- Targeted lookups that use `CanAccessWarehouse(...)` also pass for Super.

Residual behavior: `WarehouseIds` remains empty in Bootstrap/UI data for Super sessions. This does not block business services because authorization uses `IsAdmin`/`CanAccessWarehouse`, but UI components that display assigned warehouse ids may show an empty list for Super. That is display semantics, not a permission failure.

Proposed fix: none required for SP-006 unless product requirements want the UI to display all warehouses for Super sessions. If that is desired, the smallest UI/API fix would be to have Bootstrap return a separate `isSuper`/`authMode` signal or populate display-only warehouse data for Super without adding warehouse claims.

## INV-001 - Inventory check lacks inventory transaction rows

Date: 2026-05-31

### Source Documents

Requested source document `docs/SystemAudit/InventoryConsistencyAudit.md` was not present in the workspace. The same INV findings were verified from:

- `docs/SystemAudit/InventoryBusinessLogic.md`
- `docs/SystemAudit/KnownIssues.md`

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/InventoryCheckService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Verification

INV-001 still existed in source.

`InventoryCheckService.ScanBatchAsync`:

- known `Extra` items created `ItemInstance`, `CurrentItemLocation`, and stock balance `+1`, but did not create an `InventoryTransaction`;
- `WrongLocation` lines updated `CurrentItemLocation`, adjusted source/target stock balances, and wrote movement history, but did not create an `InventoryTransaction`.

`InventoryCheckService.FinalizeAsync`:

- `Missing` items wrote an inventory-check line, decremented stock, updated current location/status, and wrote movement history, but did not create an `InventoryTransaction`.

INV-002 and INV-003 were also verified as still present, but were not implemented in this task:

- INV-002: duplicate scan validation is still bin-based through `ActualBinLocationId`.
- INV-003: missing finalize history still records `OldStatus = ItemStatus.InStock` regardless of actual old status.

### Root Cause

Inventory Check was implemented as a session correction workflow. Its stock and location side effects were added directly in scan/finalize paths, but the transaction-table side effect used by inbound, move, adjustment, borrow, and repair workflows was not mirrored for inventory-check corrections.

### Data Consistency Risk

New inventory-check corrections could change `CurrentItemLocation` and `StockBalance` without corresponding `InventoryTransaction` records. Transaction-based reports could therefore disagree with current stock/location state and movement history.

### Summary

Inventory Check now writes `InventoryTransactionType.InventoryCheck` rows for new inventory-check effects:

- known `Extra`: one transaction with `QuantityDelta = +1` at the found warehouse/bin;
- `WrongLocation`: one movement-style transaction with `QuantityDelta = 0` at the corrected warehouse/bin;
- `Missing`: one transaction with `QuantityDelta = -1` at the previous warehouse/bin.

Unknown item-type extra lines still cannot create an inventory transaction because there is no `ItemId` or `ItemInstanceId` to reference.

### Migration / Backfill Impact

No schema migration is required because `InventoryTransactionType.InventoryCheck` already exists.

Existing finalized inventory-check sessions may still lack transaction rows. If transaction-based reporting must include historical inventory checks, a separate backfill should generate missing inventory-check transactions from `InventoryCheckLines`, movement history, and current document metadata where reconstructable.

### Regression Risk

Low to medium.

The change adds reporting/audit rows for new inventory-check corrections without changing session lifecycle, stock balance math, current location updates, movement history, or finalize behavior. Reports that count `InventoryTransactions` will now include new inventory-check corrections going forward.

### Validation Result

- Verified INV-001, INV-002, and INV-003 against source code.
- Implemented INV-001 only.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## INV-003 - Inventory check missing history records incorrect old status

Date: 2026-06-01

### Source Documents

Requested source document `docs/SystemAudit/InventoryConsistencyAudit.md` was not present in the workspace. The same INV-003 finding was verified from:

- `docs/SystemAudit/InventoryBusinessLogic.md`
- `docs/SystemAudit/KnownIssues.md`

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/InventoryCheckService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Verification

INV-003 still existed in source.

`InventoryCheckService.FinalizeAsync` selected unscanned active warehouse items with statuses `InStock`, `Normal`, `Damaged`, or `Scrapped`. During missing handling it:

- decremented stock using the current item status,
- cleared the current bin location,
- changed the item status to `Lost` only when the old status was `Normal` or `InStock`,
- wrote movement history with hardcoded `OldStatus = ItemStatus.InStock` and `NewStatus = ItemStatus.Lost`.

This meant `Damaged` and `Scrapped` missing items could have movement history showing an `InStock -> Lost` transition even though their actual status did not make that transition.

### Root Cause

The finalize path did not snapshot the actual pre-finalize item status before applying missing-item status rules. It used hardcoded movement-history statuses instead of the actual old status and resulting item status.

### Audit / Data Consistency Risk

Movement history could misrepresent the previous item state and the actual status transition. Audit trails and reports based on `ItemMovementHistory.OldStatus` / `NewStatus` could show incorrect lifecycle evidence for missing inventory-check items.

### Summary

Inventory Check finalize now captures `oldStatus = missingInstance.Status` before applying missing-item state changes and writes movement history with:

- `OldStatus = oldStatus`,
- `NewStatus = missingInstance.Status` after the existing status update rule.

This preserves the existing Inventory Check workflow and status-transition behavior. It only corrects the movement-history audit values for new finalize operations.

### Migration / Backfill Impact

No schema migration is required.

Existing finalized inventory-check movement history may already contain incorrect `OldStatus` / `NewStatus` values. Historical correction requires a separate audit backfill if production reporting needs exact past status transitions. Safe backfill may require reconstructing actual old status from prior movement history or other snapshots; this implementation does not alter historical rows.

### Regression Risk

Low.

The change is limited to audit history values written during missing finalize. It does not change stock balance math, current location updates, item status update rules, transaction creation from INV-001, session lifecycle, or finalize behavior.

### Validation Result

- Verified INV-003 against source code.
- Implemented INV-003 only.
- Did not modify INV-001 or start INV-002.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## INV-002 - Inventory check duplicate validation is bin-based, not item-based

Date: 2026-06-01

### Source Documents

`docs/SystemAudit/InventoryBusinessLogic.md` — authoritative definition:

> `ScanBatchAsync` rejects another line if the same `ActualBinLocationId` already exists in the document. This assumes one active item per bin. If any warehouse area allows multiple serials per bin, inventory check cannot scan them.

Note: `docs/SystemAudit/KnownIssues.md` describes INV-002 differently (matching the INV-003 finding). The `InventoryBusinessLogic.md` definition was used as authoritative.

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/InventoryCheckService.cs`
- `docs/SystemAudit/ImplementationReport.md`

### Verification

INV-002 was confirmed present in source before this session's edits. The original `ScanBatchAsync` contained the following check inside the per-line loop:

```csharp
if(await _db.InventoryCheckLines.FirstOrDefaultAsync(x =>
    x.InventoryCheckDocumentId == document.Id &&
    x.ActualBinLocationId == actualBin.Id, cancellationToken) != null)
{
    return ServiceResult<ScanBatchResultDto>.Fail($"BinCode is duplicated in this document.");
}
```

This check rejected a scan if any existing line for the session already used the same `ActualBinLocationId`. It was the only gate enforcing a one-serial-per-bin restriction.

### Root Cause

Inventory Check scan logic assumed physical bin occupancy is exclusive (one item per bin). The bin-level duplicate guard was added to enforce this assumption. However, the item/serial level duplicate protection (`alreadyScannedInstanceIds` hash set) already correctly prevents the same serial from being scanned twice, making the bin-level guard redundant and overly restrictive.

### Business Impact

Any warehouse area that holds more than one serial number per bin location could not complete an inventory check. The second serial found in an already-scanned bin would cause the entire batch to fail with "BinCode is duplicated in this document."

### Data Consistency Risk

Low. Removing the bin-level check does not affect what is written to the database. Item/serial duplicate protection via `alreadyScannedInstanceIds` prevents duplicate `InventoryCheckLine`, `CurrentItemLocation` update, `StockBalance` delta, `ItemMovementHistory`, and `InventoryTransaction` rows for the same serial. The bin check had no role in any of those side effects.

### Summary

The bin-based `ActualBinLocationId` duplicate check (4 lines) was removed from `ScanBatchAsync`. The existing item-level `alreadyScannedInstanceIds` mechanism (lines 98–101 and 168–173 in the corrected file) remains intact and provides complete serial-level duplicate prevention across and within scan batches.

A comment was added at the removal point to document the INV-002 fix.

### Migration / Backfill Impact

No schema migration is required. No existing data is modified. Existing finalized inventory check sessions are unaffected.

### Regression Risk

Low. The only behavioral change is that two different serials can now be recorded as found in the same bin within one session. Same-serial duplicate prevention is fully preserved.

### Validation Result

- Verified INV-002 against source code.
- Confirmed `alreadyScannedInstanceIds` fully covers serial-level duplicate prevention.
- Confirmed removing the bin check does not expose any stock/location/transaction duplication risk.
- Removed the 4-line bin-based duplicate check from `ScanBatchAsync`.
- File was rewritten to clean state (due to intermediate edit tool instability) preserving all prior fixes (INV-001 transactions, INV-003 old status capture).
- Build pending (see below).

## LOC-006 - Import template headers are not localized on generation

Date: 2026-06-01

### Source Documents

- `docs/SystemAudit/AI_WORK_CONTEXT.md`
- `docs/SystemAudit/LocalizationAudit.md`

### Files Changed

- `src/ERP.Inventory.Infrastructure/Services/ImportExportService.cs`
- `docs/SystemAudit/ImplementationReport.md`
- `PROJECT_STATUS.md`

### Current Logic

`TemplateAsync` is the only `/Import/Template` generation path. It resolves the canonical header list from `ImportHeaders`, builds sample rows through `TemplateRows(importType)`, and writes the workbook through `SimpleExcel.CreateWorkbook`.

Before this fix, `TemplateAsync` passed the canonical `ImportHeaders` array directly to `CreateWorkbook`, so generated templates used English-like schema keys such as `DocumentNo`, `WarehouseCode`, and `SerialNumber`.

The import parser already canonicalizes uploaded headers through `CanonicalizeImportRow` and `HeaderAliases`. `HeaderAliases` maps both canonical headers and localized `ExcelResources` values back to the canonical import keys used by validation and confirmation.

### Root Cause

Import template generation did not use the existing Excel localization helper. Export workbooks already use `Headers(user, ...)` / `ExcelText(user, ...)`, but import templates bypassed that path.

### Summary

`TemplateAsync` now keeps `ImportHeaders` as the canonical source of schema, order, validation, and parser behavior, but maps only the generated workbook header labels through `ExcelText(user, header)` before calling `SimpleExcel.CreateWorkbook`.

This localizes template headers where `ExcelResources` contains a translation and safely falls back to the canonical header key where a translation is not present.

Import behavior is unchanged:

- canonical headers are still accepted;
- localized aliases are still accepted;
- uploaded rows are still converted back to canonical keys before validation and confirmation;
- required-column checks and import business workflows remain based on canonical keys.

### Migration / Backfill Impact

No database migration is required. No existing import batches or uploaded files are modified.

### Compatibility Risk

Low to medium.

Existing files using canonical headers continue to import because canonical aliases remain supported. Newly downloaded templates may display localized headers, which can affect users or external automation that visually expects canonical English-like column names, but parser compatibility is preserved.

### Validation Result

- Confirmed the change is scoped to import template header display.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## INV-002 / LOC-001 Re-verification Addendum

Date: 2026-06-01

### Source Documents

- `docs/SystemAudit/AI_WORK_CONTEXT.md`
- `docs/SystemAudit/SESSION_HANDOFF.md`
- `docs/SystemAudit/ImplementationReport.md`
- `docs/SystemAudit/LocalizationAudit.md`

`docs/SystemAudit/InventoryConsistencyAudit.md` was requested but is not present in the workspace. INV-002 was re-verified directly from `InventoryCheckService.cs` and the existing implementation report entry.

### INV-002 Verification

INV-002 no longer exists in source.

`InventoryCheckService.ScanBatchAsync` no longer rejects duplicate scans by `ActualBinLocationId`. The previous `"BinCode is duplicated in this document."` bin-level validation is absent.

Current duplicate protection is item-instance based:

- existing non-missing `InventoryCheckLine.ItemInstanceId` values are loaded into `alreadyScannedInstanceIds`;
- each known scanned item instance is skipped if already present;
- new known item instances are added to the set before side effects are written.

This is sufficient for serial-level duplicate protection because known inventory-check side effects are keyed to a specific `ItemInstanceId`. Multiple different serials in the same bin are now allowed, while the same serial cannot create duplicate scan lines, stock/location changes, movement history, or inventory transactions in the same session.

Remaining edge case: unknown extra rows without an existing `ItemInstanceId` are not covered by `alreadyScannedInstanceIds` because no item instance exists yet when the item type is unknown. This is existing behavior and is outside INV-002's bin-based duplicate issue.

Migration/backfill impact: no schema migration or data backfill is required. Existing finalized inventory-check sessions are unchanged.

Regression risk: low. The corrected behavior only removes the one-bin-one-serial assumption and preserves same-item duplicate protection.

### LOC-001 Verification

LOC-001 no longer exists in source.

The current language switch path in `app.js`:

- captures draft state through `UI.captureLanguageSwitchDraft()`;
- stores it in `sessionStorage`;
- posts to `/App/Language`;
- reloads localization resources and lookups;
- re-renders the current route;
- restores the draft through `UI.restoreLanguageSwitchDraft()`.

`ui.js` contains the draft capture, storage, cleanup, and restore helpers. `router.js` returns the page render promise from `Router.go(...)`, allowing the language handler to wait for the route render before restoring fields.

Correctness review:

- no `window.location.reload()` remains in the language switch path;
- duplicate-name table rows are restored by occurrence index;
- operation and quantity line counts are recreated before field restore;
- password, anti-forgery, and file input values are not persisted;
- selected-file and drawer-open cases preserve the current DOM instead of replacing non-restorable user state;
- the error path clears the draft and rolls back the selected language value.

Files changed for LOC-001:

- `src/ERP.Inventory.Web/wwwroot/erp/js/app.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/core/ui.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/core/router.js`
- `docs/SystemAudit/ImplementationReport.md`

Migration/backward compatibility impact: no database migration, no backfill, and no server contract change. The supported language model remains `vi` / `en` / `zh`. Import/export behavior is unchanged.

Regression risk: low to medium. Normal screens re-render and restore named fields. Screens without editable fields behave as before. File selections are intentionally preserved by avoiding the route re-render because browsers do not allow programmatic file-value restoration.

### Validation Result

- Re-verified INV-002 from source.
- Re-verified LOC-001 from source.
- Ran bundled Node syntax checks for `app.js`, `ui.js`, and `router.js`; all passed.
- `dotnet build ERP.Inventory.sln` pending at the time of this report update.

## LOC-001 - Language switching reloads the page and loses unsaved form state

Date: 2026-06-01

### Source Documents

`docs/SystemAudit/LocalizationAudit.md`

### Files Changed (already in working tree — no new changes made)

- `src/ERP.Inventory.Web/wwwroot/erp/js/app.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/core/ui.js`
- `docs/SystemAudit/ImplementationReport.md`

### Verification

LOC-001 was found **already fully implemented** in the working tree before any code changes were made in this session. No code modifications were required.

### Root Cause (original)

The `#languageSelect` change handler previously called `window.location.reload()`, discarding all unsaved form state.

### Implementation Found in Working Tree

**`app.js` (lines 29–54):** The language switch handler was replaced with an AJAX-based flow:

1. Captures form state with `UI.captureLanguageSwitchDraft()` before any changes.
2. Stores draft in `sessionStorage` via `UI.storeLanguageSwitchDraft()`.
3. Posts language preference to `/App/Language`.
4. Reloads localization resources via `loadResources(lang)`.
5. Reloads lookups via `loadLookups()`.
6. Re-renders the menu via `renderMenu()`.
7. Skips restore and clears draft if drawer is open or file input has a selection.
8. Re-navigates to the current screen via `Router.go(Router.current)` to re-render with new labels.
9. Restores form state via `UI.restoreLanguageSwitchDraft(draft)`.
10. On error: clears draft, rolls back `AppState.lang` and the select value, shows a toast.

**`ui.js` (lines 244–339):** Four new functions on `window.UI`:

- `captureLanguageSwitchDraft()`: snapshots all form field values, drawer state, active quantity view, and line counts.
- `storeLanguageSwitchDraft(draft)`: writes to `sessionStorage`.
- `clearLanguageSwitchDraft()`: removes from `sessionStorage`.
- `restoreLanguageSwitchDraft(draft)`: re-adds line rows, restores field values by occurrence index (safe for duplicate-name table rows), triggers input/change events, cleans up.

No `window.location.reload()` call exists anywhere in the language switch path.

### Correctness Review

Reviewed and found correct:

- Page no longer reloads on language switch.
- Draft captured before any API call.
- Draft cleared after successful restore and on error.
- Drawer-open and file-input edge cases handled by early return.
- Server language preference still persisted via POST.
- Field restore uses occurrence-index matching for table rows with duplicate `name` attributes.
- CSRF tokens and password fields excluded from draft.
- Error path rolls back UI state.

No correctness issues found.

### Migration / Backfill Impact

No database migration is required. No data migration is required. This is a frontend-only change.

### Regression Risk

Low. The AJAX switch flow re-renders the current screen and restores state. Screens that do not expose named inputs will behave identically to before. The error path rolls back to the previous language.

### Validation Result

- Verified LOC-001 was already implemented before this session started.
- Reviewed `app.js` and `ui.js` implementation for correctness — no issues found.
- No code changes made for LOC-001.
- Build pending (see below).

## LOC-002 - Hardcoded frontend strings remain

Date: 2026-06-01

### Source Documents

- `docs/SystemAudit/AI_WORK_CONTEXT.md`
- `docs/SystemAudit/LocalizationAudit.md`
- `PROJECT_STATUS.md`

### Files Changed

- `src/ERP.Inventory.Web/wwwroot/erp/js/pages/import.page.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/pages/operation.page.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/pages/reconciliation.page.js`
- `src/ERP.Inventory.Web/Services/LocalizationCatalog.cs`
- `docs/SystemAudit/ImplementationReport.md`
- `PROJECT_STATUS.md`

### Current Logic

Frontend localization resources are loaded into `AppState.resources`. User-visible frontend text should be rendered through `UI.t(...)`, `UI.msg(...)`, enum helpers, or existing reconciliation keys so missing translations do not leak raw English text to Vietnamese/Chinese users.

### Root Cause

Several frontend confirmation, toast, and validation fallback paths still used literal strings or native browser confirmation. Those paths bypassed the localization catalog or prevented the existing catalog keys from being used consistently.

### Summary

LOC-002 was implemented only for the known frontend hardcoded-string finding. No business workflows, lifecycle behavior, import parsing, document posting, or server-side validation logic were changed.

Fixed LOC-002 frontend findings:

- Import page upload, validate, and confirm success fallback toasts now use stable catalog/server message keys through `UI.msg(...)`.
- Import confirm dialog title is localized through `UI.t('Confirm Import')`.
- Import empty-state text is localized through `UI.t('No data')`.
- Reconciliation create/session/run failure fallbacks now use `UI.msg(...)` with the cataloged `Request failed.` fallback.
- Reconciliation import result summary now uses the existing `recon.importsummary` localized template.
- Reconciliation import file label now uses the cataloged `File` key.
- Reconciliation run confirmation no longer uses native `confirm`; it uses `UI.confirm(...)` with localized title/body.
- Operation attachment upload and missing-session-id validation fallbacks now use localized labels/messages.
- Added missing `Session created but documentId is missing.` localization entries for Vietnamese, English, and Chinese.

Existing current-tree LOC-002 coverage was preserved for delete/rebuild confirmations, setup/inventory hard-delete text, and document action messages that were already routed through `UI.t(...)` / `UI.msg(...)`.

### Migration / Backfill Impact

No database migration is required. No production data is modified.

### Compatibility Risk

Low.

The changes are presentation-only. Existing API contracts, request payloads, response handling, and business workflows are unchanged. Reconciliation run confirmation now uses the app confirmation modal instead of the browser-native confirm dialog.

### Validation Result

- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.
- Attempted Node `--check` syntax validation for edited JavaScript files, but `node.exe` returned `Access is denied` in this environment even when rerun with escalation.

## PERF-001 - Startup runs database migration automatically

Date: 2026-06-01

### Files Changed

- `src/ERP.Inventory.Web/Program.cs`
- `docs/SystemAudit/ImplementationReport.md`
- `PROJECT_STATUS.md`

### Current Logic

Application startup previously called `db.Database.MigrateAsync()` unconditionally before seeding security data.

### Root Cause

Database migration execution was coupled to application boot. That simplified development startup but created production deployment risk because schema migration work could run during web application startup.

### Summary

Startup migrations are now controlled by configuration key `RunStartupMigrations`.

If `RunStartupMigrations` is explicitly configured, that value is used. If it is not configured, startup migrations run only in Development. Production deployments can therefore use an external migration/deployment step while preserving the development convenience path.

Security seeding and development sample-data seeding behavior were left unchanged.

### Migration / Backfill Impact

No database schema migration is required for this code change.

Production environments that still depend on app startup to apply EF migrations must either configure `RunStartupMigrations=true` or move migration execution into the deployment process before starting the app.

### Compatibility Risk

Low to medium.

Runtime behavior is safer for production, but deployments that implicitly relied on startup migration need a configuration or deployment-process adjustment.

### Validation Result

- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.

## PERF-002 - Dashboard/report/list queries may scan large history tables

Date: 2026-06-01

### Files Changed

- `src/ERP.Inventory.Infrastructure/Data/InventoryDbContext.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/20260601070918_AddPerf002ReportingIndexes.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/20260601070918_AddPerf002ReportingIndexes.Designer.cs`
- `src/ERP.Inventory.Infrastructure/Data/Migrations/InventoryDbContextModelSnapshot.cs`
- `docs/SystemAudit/ImplementationReport.md`
- `PROJECT_STATUS.md`

### Summary

Added only the scoped EF indexes requested for PERF-002:

- `AuditLogs(CreatedAt)`
- `AuditLogs(ReferenceNo)`
- `ImportBatchRows(ImportBatchId, RowNumber)`
- `InventoryTransactions(PostedAt)`
- `InventoryTransactions(DocumentNo)`
- `ItemMovementHistories(PerformedAt)`
- `ItemMovementHistories(DocumentNo)`

No additional indexes were added and no query/business workflow was changed.

### Migration Impact

Generated migration: `20260601070918_AddPerf002ReportingIndexes`.

Because SQL Server cannot index `nvarchar(max)` columns, EF scaffolding narrows the newly indexed string columns to `nvarchar(450)`:

- `AuditLogs.ReferenceNo`
- `InventoryTransactions.DocumentNo`
- `ItemMovementHistories.DocumentNo`

The existing `ImportBatchRows(ImportBatchId)` index is replaced by the requested composite `ImportBatchRows(ImportBatchId, RowNumber)` index, which still supports filtering by `ImportBatchId` as the leading column.

Index creation should be scheduled carefully on production-sized history/audit/transaction tables because it can add temporary database load and will add small ongoing write overhead.

### Compatibility Risk

Low to medium.

The change is schema/index-only and preserves application behavior. The main compatibility consideration is the `nvarchar(450)` limit on document/reference fields that were previously `nvarchar(max)`. Existing document/reference numbers are expected to be far below this length.

### Validation Result

- Generated EF migration with `dotnet ef migrations add AddPerf002ReportingIndexes`.
- Ran `dotnet build ERP.Inventory.sln`.
- Build succeeded with existing warnings only.
