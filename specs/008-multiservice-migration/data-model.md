# Data Model: FlowVision Multiservice Modernization

**Feature**: 008-multiservice-migration | **Date**: 2026-11-11

The platform does not own new business tables. It maps to the existing `FLOWVISION2` schema, which the desktop applications also use (FR-007). Table names below come from the migration scripts in `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/`. Names marked **(verify)** appear in code but were not in the CREATE TABLE scan of versions 2.0.24 to 2.0.35, so check them against the base scripts before mapping.

## Schema rules added by this feature

1. **Row version (clarification Q3)**: every shared table gets a `ROWVERSION` column, added by one versioned script. Web queries choose which reads and writes check it.
   - Script: `db_mod_FlowVision_2.0.{N+1}.sql`, where N+1 is the next free number after 2.0.35 (confirm before creating).
   - Header comment must include rollback steps (constitution Principle V).
   - Test on `FLOWVISION2_TEST` first. Confirm desktop `INSERT` statements and `SELECT *` bindings still work.
2. **Schema version check (FR-006)**: a single table or view holds the applied version. Each service reads it at start and refuses to run on a mismatch. Creating that table is part of the same script (name to decide in ADR-001).
3. **Audit (FR-011)**: every write records the user, date and time. Existing tables with `*_USER`, `*_DATE` columns are reused where present; missing ones are added by the same script only if the table has none.
4. **DDL placement** (constitution Principle V, version 2.0.0): DDL lives only in `.sql` scripts and EF Core migration classes. Services never run it at runtime.

## Entities (from spec.md Key Entities)

### Production Order → `REQUEST` (verify)

| Attribute | Notes |
|---|---|
| Identity | Existing primary key. Uniqueness as in the desktop application. |
| Status | Values from `REQUEST.REQ_STATUS` (full enum is OQ-007, open). Read-only enum mapping until OQ-007 closes. |
| Concurrency | `ROWVERSION` (rule 1). Web saves check it on the queries selected in implementation. |
| Relationships | Has many `REQUEST_OPERATIONS` (verified). Assigned to workstation via operations. |

### Operation → `REQUEST_OPERATIONS` (verified) and `TROLEY_REQUEST_OPERATIONS`

Relationship: one production order has many operations; each operation has a planned and actual time. Status changes come from the scan flow (User Story 1).

### Trolley / Trolley Operation → `TROLEY_REQUEST`, `TROLLEY_OPERATIONS`, `TROLLEY_QRCODES`

- `TROLEY_REQUEST` (created in 2.0.22; carried forward in later scripts) is the trolley-to-order link (verify column names).
- `TROLLEY_OPERATIONS` (created in 2.0.35) holds the operations a trolley goes through.
- `TROLLEY_QRCODE` (live, `TRQR_*`) maps QR/barcode codes to trolleys. `TROLEY_QRCODES` does not exist on the live DB.
- The scan flow resolves a code via `QRCODE` (2.0.34, 2.0.35) and `TROLLEY_QRCODE`.

### Work Record (Time Record) → `TIME_OF_OPERATIONS_QRCODES` (2.0.35) and `TIME_OF_OPERATIONS` (verified)

- Fields the spec requires: operator, operation, start time, stop time, elapsed time, cost, good quantity, defective quantity.
- Cost figures must equal the desktop calculation (FR-018); the calculation is a pure function with golden tests.
- Validation: a record cannot be stopped without a start; unknown codes create no record (User Story 1, scenario 3).

### User / Role → `USERS`, `USERS_FUNCTIONS` (2.0.35)

- `USERS` holds accounts and `USERS_PASSWORD` (`varchar(64)`, widened to `varchar(128)` by 2.0.36). Hashing and legacy reset: OQ-005 (confirmed, `open-questions.md`). Role code `USERS_ROLE`: `A` (administrator), `B` (foreman), `U` (operator); mapping: OQ-004 (confirmed).
- `USERS_FUNCTIONS` holds the per-function permissions used for FR-010.
- Separate sign-in per web interface (clarification Q1-B): no session shared between interfaces.

### Document → `DOCUMENT` (live, `DOC_*` columns, 15 columns)

- Linked to production orders, items, products, projects, operations (`DOC_*_ID` and `DOC_*_NR`). FlowDokumenty's `DOKUMENT` (`DOK_*`) does not exist on the live DB (OQ-003).

### Process / Department → `PROJECT`, `PRODUCTS`, `PRODUCTS_ITEMS`, `DEPARTAMENTS` (verified where noted)

- `PROJECT` (live, `PROJ_*`) holds projects. `PROJECT_NAG` (live) holds project data, not defects. `PRODUCT` (singular) does not exist; use `PRODUCTS` (live).
- Department: `DEPARTAMENTS` (live). Process definitions are not managed by the platform in this release (OQ-003, option C). `PROCESY` and `DZIAL` do not exist on the live DB.

### Machine / Workstation → `MACHINE` (verified), `MACHINE_FAILURE_TYPE`, `MACHINE_FAILURE_EVENT` (2.0.35)

- Operations are performed on workstations; scans are recorded against them.

### Defect (SNAG) → `SNAG` (live, `SNAG_*`)

- Recorded against an operation or production order.
- `MACHINE_FAILURE_EVENT` records machine failures, separate from defects.

## Transitions and states

- **Production order**: states come from `REQUEST.REQ_STATUS` (OQ-007). The web interface may only move an order to the next state allowed by the desktop rules; the rule set is confirmed in planning with the product owner.
- **Operation**: not started → in progress (start scan) → stopped (stop scan). A stop without a start is rejected.

## Validation rules (from spec)

- Every write has user and timestamp (FR-011).
- Concurrent web saves are rejected when the row version has changed (FR-012, scope per rule 1).
- Unknown code: no record created or changed (User Story 1, scenario 3).
- Schema version mismatch: service does not start (FR-006).

## Open items

- Names are confirmed against the live `FLOWVISION2` schema (2026-11-12) except the process entity: `DZIAL` and `PROCESY` are absent, and `PRODUCT` (singular), `DOKUMENT`, and `TROLEY_QRCODES` are absent too. The process storage decision is open (OQ-003).
- OQ-007 resolved (`open-questions.md`). OQ-004 and OQ-005 confirmed by the product owner.
