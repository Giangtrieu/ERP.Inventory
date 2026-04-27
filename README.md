# ERP Inventory Management System

Full-stack scaffold aligned with the BA/TDD and UI blueprint:

- Backend: ASP.NET Core MVC, layered architecture, Application Services, transaction-safe warehouse operations.
- Frontend: Bootstrap + jQuery enterprise shell, Tracking-first screen, transaction forms, status badges, append-only timeline.
- Database: Entity Framework Core Code First with SQL Server, explicit indexes and constraints, seed data, core SQL schema.

## Solution Structure

```text
ERP.Inventory
  src/
    ERP.Inventory.Domain
    ERP.Inventory.Application
    ERP.Inventory.Infrastructure
    ERP.Inventory.Web
  database/
    schema.sql
  docs/
```

## Core Business Rules

- One item instance has one current location.
- Every status/location change writes `ItemMovementHistory`.
- No `PendingApproval`; valid operations are saved and posted immediately.
- `ApprovedBy` is the confirming user, often the same as `CreatedBy`.
- Import validates all rows before committing real data.
- Large tables use server-side paging/filtering.

## Run

```powershell
dotnet restore .\ERP.Inventory.sln --configfile .\NuGet.Config --ignore-failed-sources
dotnet build .\ERP.Inventory.sln --no-restore
dotnet run --project .\src\ERP.Inventory.Web\ERP.Inventory.Web.csproj
```

Default connection string uses LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=ERPInventoryDb;Trusted_Connection=True;MultipleActiveResultSets=true
```

Set `SeedDatabase` to `true` in `appsettings.json` during development if you want the app to migrate and insert demo data on startup.

## Main Endpoints

- `GET /Tracking/Search?keyword=...`
- `GET /Tracking/History/{itemInstanceId}`
- `GET /Inventory/List`
- `POST /Inventory/Inbound`
- `POST /Inventory/MoveLocation`
- `POST /Inventory/Adjust`
- `POST /Repair/SendToRepair`
- `POST /Repair/ReceiveFromRepair`
- `POST /Borrow/Lend`
- `POST /Borrow/Return`
- `GET /Dashboard/Summary`
- `GET /Dashboard/StockByWarehouse`
