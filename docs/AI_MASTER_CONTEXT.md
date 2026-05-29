# ERP B34 MASTER CONTEXT

## Stack

- ASP.NET Core MVC
- EF Core
- Razor
- jQuery
- Bootstrap

## Architecture Constraints

- Preserve current architecture
- Preserve current services
- Preserve current orchestration
- No CQRS
- No Event Sourcing
- No schema redesign unless required
- Minimal invasive changes

---

# Inventory Models

## LocationTracked

Source of truth:

- InventoryTransaction
- ItemMovementHistory

Projections:

- CurrentItemLocation
- StockQuantity

Flows:

- Inbound
- MoveLocation
- Borrow Send
- Borrow Return
- Repair Send
- Repair Return
- Adjustment

## QuantityOnly

Source of truth:

- QuantityInventoryTransaction

Projection:

- QuantityStockBalance

Currently migrating from:

SN based

to

PN + Quantity based

---

# Stable Areas

Do not modify unless required:

- Inbound posting
- MoveLocation posting
- Location rebuild logic
- CurrentItemLocation restoration

---

# Lifecycle Rules

Normal Edit:

- diff based
- selective rollback
- selective replay

Do NOT:

- full delete
- full repost

Rebuild:

- recovery only

Delete:

- transactional reverse

All operations:

- single transaction

---

# Current Priorities

P1:
Borrow / Repair lifecycle correctness

P2:
Legacy data migration support

P3:
LogErrorSystem

P4:
QuantityOnly migration

P5:
Dashboard quantity charts

---

# Required Workflow

1. Audit
2. Root cause
3. Schema gap analysis
4. Migration plan
5. Implement
6. Verify

Never skip audit.

Never jump directly into large rewrites.
