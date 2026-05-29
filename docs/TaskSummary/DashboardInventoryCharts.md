# Dashboard Inventory Charts

## Files Changed

- `src/ERP.Inventory.Application/DTOs/DashboardDtos.cs`
- `src/ERP.Inventory.Infrastructure/Services/DashboardService.cs`
- `src/ERP.Inventory.Web/wwwroot/erp/js/pages/dashboard.page.js`
- `src/ERP.Inventory.Web/wwwroot/erp/js/pages/quantity-inventory.page.js`
- `src/ERP.Inventory.Web/wwwroot/erp/app.css`
- `src/ERP.Inventory.Web/Services/LocalizationCatalog.cs`

## Queries

- `QuantityStockBalances`
  - scoped by selected warehouse and current user warehouse permissions.
  - filtered to `Quantity > 0`.
  - aggregate total quantity with `SUM(Quantity)`.
  - aggregate active item/SN lots with distinct `(ItemId, SnCode)`.
- Quantity by ItemCode
  - source: `QuantityStockBalances` joined by EF navigation to `Items`.
  - grouped by `Items.ItemCode`.
  - projected as total quantity plus percentage of total quantity.
- Quantity by ItemCategory
  - source: `QuantityStockBalances` joined by EF navigation to `Items` and `ItemCategories`.
  - grouped by `ItemCategories.CategoryCode + " - " + ItemCategories.Name`.
  - projected as total quantity plus percentage of total quantity.

No per-row lookup or N+1 query was added; chart rows are produced by grouped database queries.

## DTO Changes

- Added `QuantitySummaryDto.QuantityByItemCode`.
- Added `QuantitySummaryDto.QuantityByItemCategory`.
- Added `ChartPointDto.Percentage`.

## Controller Changes

- No new controller endpoint was required.
- Existing `GET /Dashboard/QuantitySummary` now returns the two new pie chart datasets inside the existing quantity dashboard payload.

## UI Changes

- Preserved the current dashboard and existing quantity summary cards/charts.
- Added two responsive SVG pie charts under the existing `Quantity Inventory Summary` dashboard section:
  - Inventory Quantity Distribution by ItemCode.
  - Inventory Quantity Distribution by ItemCategory.
- Pie legends display total quantity and percentage.
- ItemCode chart supports drill-down by clicking a slice or legend row. It routes to `quantity-inventory` and pre-fills the existing keyword filter with the selected item code.
- Added localized labels in Vietnamese, English, and Chinese.

## Verification Steps

- `dotnet build ERP.Inventory.sln`
- `node --check src/ERP.Inventory.Web/wwwroot/erp/js/pages/dashboard.page.js`
- `node --check src/ERP.Inventory.Web/wwwroot/erp/js/pages/quantity-inventory.page.js`

Build completed successfully. The default `node.exe` on PATH was blocked by Windows access policy, so syntax verification used the bundled Codex runtime Node.js executable.
