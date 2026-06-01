# Super Password Audit

## Current Logic

- `SuperAdminSecurity.SuperAdminPassword` is initialized from configuration key `SuperAdminPassword`.
- If the configuration key is missing, it falls back to a hardcoded default in code.
- Login first loads an active `SystemUser` by submitted username.
- If the submitted password matches the super password, the code signs in with cookie auth and adds claims:
  - `NameIdentifier` = selected user's id
  - `Name` = selected user's username
  - `display_name` = selected user's display name
  - `language` = selected user's preferred language
  - another `display_name` = `SuperAdmin`
  - role `SystemSuperAdmin`
  - `AuthMode = Super`
- `SuperAdminAuthorizationHandler` succeeds all role requirements if `AuthMode = Super`.
- Super login writes an `AuditLog` with action `SuperLogin`.

## Problems Found

### SP-001 - Hardcoded fallback super password exists

If configuration does not override it, the system uses the in-code value.

### SP-002 - Super login can crash when username does not match an active user

The code dereferences `user.Id`, `user.UserName`, `user.DisplayName`, and `user.PreferredLanguage` inside the super password branch before checking `user == null`.

### SP-003 - Super password does not truly bypass username lookup

Although intended as an emergency bypass, the current implementation still requires an active username to be found first.

### SP-004 - `SuperOnly` policy appears inconsistent with super claims

The policy requires `ClaimTypes.Name = SuperAdmin`, but super login sets `ClaimTypes.Name` to the selected user's username. The role bypass handler only handles role requirements, not this claim requirement.

### SP-005 - Duplicate `display_name` claims are emitted

Both the selected user's display name and `SuperAdmin` are added as `display_name`. Consumers that read the first claim may not display or audit the intended identity.

### SP-006 - Warehouse permissions are empty in super mode

`CurrentUserService` reads warehouse permissions only from `warehouse_id` claims. Super login does not add warehouse claims. Role authorization is bypassed, but business services using `user.CanAccessWarehouse(...)` may still reject operations unless `CurrentUserContext` treats super mode specially.

## Root Cause

- SuperPassword was implemented inside the normal login flow rather than as a separate authentication path.
- Role-level bypass was added, but business-level permission checks and custom policies were not fully aligned.
- Secret management relies on configuration but keeps an unsafe fallback.

## Proposed Solution

- Remove the hardcoded fallback. Require `SuperAdminPassword` from protected configuration/secret storage.
- Check super password before dereferencing the selected user, or define explicit behavior when username is blank/unknown.
- Emit a clear principal:
  - `NameIdentifier = SuperAdmin` or a dedicated emergency id.
  - `Name = SuperAdmin`.
  - one `display_name` claim.
  - `AuthMode = Super`.
- Update `CurrentUserContext.CanAccessWarehouse` or service checks so super mode consistently bypasses warehouse restrictions.
- Add audit details: target username, requester IP, browser, timestamp, and auth mode.
- Consider rotating the super password and logging every use as a security event.

## Data Migration Impact

No data migration is required. Existing audit logs remain useful but may lack target user/IP details.

## Compatibility Risks

- Removing fallback password requires deployment configuration readiness.
- Changing claims can affect UI display and authorization checks.
- Enabling true warehouse bypass increases blast radius; it should be limited to emergency use with strong audit.
