ERP.Inventory AI Work Context

Purpose



This document is the master context for all AI assistants working on ERP.Inventory.



Examples:



GPT-5.5

Codex

Claude Sonnet

Gemini

Cursor AI

Copilot



Every AI must read this document before making any code changes.



CRITICAL RULE



Do NOT redesign the system.



Do NOT replace existing business workflows.



Do NOT introduce new architecture unless explicitly required.



The system is already running in production and contains real data.



The primary goal is:



Preserve business behavior.

Preserve existing data.

Fix inconsistencies.

Improve correctness.

Improve maintainability.

SOURCE OF TRUTH DOCUMENTS



The following documents are authoritative.



Read them before changing any code.



Business Logic



docs/SystemAudit/InventoryBusinessLogic.md



Contains:



Inventory workflow

Inbound workflow

Move workflow

Adjustment workflow

Inventory Check workflow

Quantity Inventory workflow

Borrow workflow

Repair workflow

Document Rules



docs/SystemAudit/DocumentTypeRules.md



Contains:



Single-use documents

Multi-use documents

Append behavior

Document number rules

Borrow / Repair Lifecycle



docs/SystemAudit/LifecycleBatchAnalysis.md



AND



docs/TaskSummary/BorrowRepairLifecycleBatch.md



These are the official source of truth.



Any Borrow/Repair behavior that matches these documents must NOT be treated as a bug.



Do not redesign lifecycle rollback.



Do not replace LifecycleBatchId logic.



Known Issues



docs/SystemAudit/KnownIssues.md



Contains confirmed issues only.



Do not create duplicate issues unless new evidence exists.



Proposed Fixes



docs/SystemAudit/ProposedFixes.md



Contains recommended implementation order.



Security



docs/SystemAudit/SuperPasswordAudit.md



Contains:



Current behavior

Risks

Required fixes

Localization



docs/SystemAudit/LocalizationAudit.md



Contains:



Current localization architecture

Known issues

Required improvements

Performance



docs/SystemAudit/PerformanceAudit.md



Contains:



Existing optimizations

Known bottlenecks

Recommended improvements

Import



docs/SystemAudit/ImportAudit.md



Contains:



Current import architecture

Differences between import and manual workflows

Required corrections

CURRENT SYSTEM ARCHITECTURE



The system uses:



Location Tracked Inventory



Entities:



ItemInstance

CurrentItemLocation

ItemMovementHistory

InventoryTransaction

StockBalance



CurrentItemLocation is current state.



ItemMovementHistory is audit trail.



InventoryTransaction is reporting transaction history.



StockBalance is reporting/cache aggregate.



Quantity Inventory



Entities:



QuantityInventoryDocument

QuantityInventoryDocumentLine

QuantityInventoryTransaction

QuantityStockBalance

DOCUMENT TYPES

Single-use Documents



Examples:



Move

Adjustment



Rules:



One DocumentNo = one document

Duplicate DocumentNo is invalid

Multi-use Documents



Examples:



Inbound

Borrow

Repair

Quantity Inventory



Rules:



One DocumentNo = one document header

New operations append batches

LifecycleBatchId identifies one operation batch

IMPORT RULE



This is extremely important.



Import must behave identically to manual operations.



The only difference allowed is:



Input source.



NOT business logic.



If manual create produces:



document

line

log

movement history

inventory transaction

stock balance

lifecycle batch



then import must produce the same effects.



LIFECYCLE RULE



Borrow and Repair lifecycle rollback is already standardized.



LifecycleBatchId is mandatory.



Delete and edit operations must only affect the intended lifecycle batch.



Never replace LifecycleBatchId with timestamp grouping.



DATA SAFETY RULE



Before any change:



Evaluate:



existing production data

migration impact

rollback strategy

rebuild requirements



Any data migration requirement must be documented.



CHANGE MANAGEMENT RULE



Before implementing:



Document:



Current Logic

Problem

Root Cause

Proposed Solution

Data Migration Impact

Compatibility Risk



If these sections are not understood, do not change code.



PRIORITY ORDER



Priority A



BR-LC-001



Priority B



IMP-002

IMP-003

IMP-004

IMP-005



Priority C



SP-001 → SP-006



Priority D



INV-001

INV-003



Priority E



LOC-001 → LOC-006



Priority F



PERF-001 → PERF-007

WHEN CONTINUING WORK



Assume previous AI work may be incomplete.



Do not restart the audit.



Do not rewrite audit documents.



Continue from existing findings.



Only add new findings if supported by source code evidence.



SUCCESS CRITERIA



A task is complete only when:



Business behavior remains consistent.

Existing data remains valid.

Audit documents remain accurate.

LifecycleBatchId behavior remains intact.

Import and manual workflows are consistent.

Build passes.

No regression is introduced.

