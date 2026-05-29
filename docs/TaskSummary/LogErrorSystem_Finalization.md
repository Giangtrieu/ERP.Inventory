# LogErrorSystem Finalization

## Scope

Continued the existing LogErrorSystem implementation without recreating entities, services, middleware, or API endpoints.

## Completed

- Added the missing EF Core migration for the existing `LogErrorSystem` entity and DbContext configuration.
- Completed Error Management UI in the existing System screen.
- Implemented `loadSystemErrors()` with SuperAdmin password unlock, keyword search, resolved/unresolved filter, paging, detail drawer, and resolve action.
- Added missing localization keys for Error Management UI labels and messages in Vietnamese, English, and Chinese.

## Migration

- `20260529115501_AddLogErrorSystem`
- Creates `LogErrorSystem`.
- Creates indexes:
  - `IX_LogErrorSystem_CreatedAt`
  - `IX_LogErrorSystem_ErrorCode`
  - `IX_LogErrorSystem_IsResolved`

## Verification Scenario

1. Configure `SuperAdminPassword` in application configuration.
2. Apply migrations by starting the app or running `dotnet ef database update`.
3. Sign in as an Admin user.
4. Trigger a controlled server exception from any MVC/API path covered by `LogErrorSystemMiddleware`.
5. Confirm the response includes an `ERR-yyyyMMdd-########` error code.
6. Open System > Error Management.
7. Enter the SuperAdmin password and unlock.
8. Verify the error appears in the list.
9. Search by error code or request path.
10. Change status filter between All, Unresolved, and Resolved.
11. Open detail and verify request, user, message, payload, and stack trace are shown.
12. Add resolution notes and mark resolved.
13. Verify the row moves to Resolved and remains searchable.

## Notes

- No duplicate entity, DbSet, service, middleware, or API endpoint was added.
- The feature still relies on the existing Admin role plus SuperAdmin password gate.
