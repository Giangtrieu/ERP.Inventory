# Localization Audit

## Current Logic

- Main localization source is `LocalizationCatalog.cs`.
- Supported languages are Vietnamese (`vi`), English (`en`), and Chinese (`zh`).
- `LocalizationCatalog.Text(language, key)` returns the localized value or the key if missing.
- The frontend loads resources through app boot/language endpoints and uses `UI.t(...)` or `UI.msg(...)` for many labels/messages.
- Enum option localization is provided by `LocalizationCatalog.AllEnumOptions`.
- Export/import Excel resources are duplicated in `ImportExportService.ExcelResources`.
- Language switching posts to `/App/Language`, rewrites the auth cookie language claim, and the frontend reloads the page.
- Frontend caches localization resources in `localStorage`.

## Problems Found

### LOC-001 - Language switching reloads the page and loses unsaved form state

The frontend language selector posts the new language and reloads `window.location`. There is no general form draft persistence before reload.

### LOC-002 - Hardcoded frontend strings remain

Several JavaScript files still pass raw English strings to `UI.confirm`, native `confirm`, or `UI.toast`. Examples include delete/rebuild confirmations, import confirmations, reconciliation messages, and setup hard delete text.

### LOC-003 - Import/export localization resources are duplicated

`ImportExportService` has a separate `ExcelResources` dictionary instead of reusing `LocalizationCatalog`. This creates drift risk between UI labels and Excel labels.

### LOC-004 - Some server messages are raw English/Vietnamese strings

Many service validation failures return literal strings. The UI often calls `UI.t` or `UI.msg`, but messages that are not in the catalog fall back to the original text.

### LOC-005 - Encoding artifacts exist in source text

Some Vietnamese/Chinese strings appear mojibake in source output. This may be a file encoding or terminal display issue, but it should be verified because broken resource text affects users directly.

### LOC-006 - Import template headers are not localized on generation

`TemplateAsync` passes canonical `ImportHeaders` directly to `SimpleExcel.CreateWorkbook`. The parser can accept localized aliases, but generated templates currently use canonical English-like keys.

## Root Cause

- Localization was added incrementally.
- Frontend state is not separated from page lifecycle.
- Excel resources and UI resources evolved independently.
- Service-layer errors are not returned as stable localization keys.

## Proposed Solution

- Add a form draft persistence layer for operation/import/edit screens before language reload.
- Prefer AJAX language switching that reloads resources and re-renders labels without full page reload.
- Replace hardcoded JS strings with `UI.t` keys and add missing keys to `LocalizationCatalog`.
- Unify Excel labels through a shared localization provider or generated resource map.
- Return stable error keys from services where possible; localize at controller/UI boundary.
- Verify source file encoding and normalize resource files to UTF-8.
- Localize `TemplateAsync` headers with `Headers(user, ...)` while preserving parser aliases for canonical and localized headers.

## Data Migration Impact

No database migration is required. If translations move from code to database later, seed/migration scripts will be needed.

## Compatibility Risks

- Localized import template headers can affect users who built automation around canonical English headers. Parser aliases reduce this risk.
- AJAX language switching requires careful re-rendering of dynamic UI state.
