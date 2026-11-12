# Open Questions: Resolution Record

**Feature**: 008-multiservice-migration | **Date**: 2026-11-11

This file closes the open questions (OQ) that blocked tasks in `tasks.md`. Each entry states the evidence, the decision, and its status:

- **Resolved**: answered from the repository (code and migration scripts). No product decision needed.
- **Confirmed**: decided by the product owner. Changing it later touches the listed tasks.
- **Blocked**: needs access to the live `FLOWVISION2` database, which was not available in this session.

## Summary

| OQ | Topic | Status | Unblocks |
|---|---|---|---|
| OQ-003 | FlowDokumenty and ProcessUser repository; document tables | Resolved (repository); Resolved from live schema (document, department); Product decision: option C (processes out of scope for the web interface) | T099, T100 unblocked |
| OQ-004 | Full role list and role mapping | Resolved (role codes); Confirmed by product owner (role-to-interface mapping) | T049, T069 |
| OQ-005 | Password hash algorithm and legacy credentials | Confirmed by product owner | T047 |
| OQ-007 | `REQUEST.REQ_STATUS` enum | Resolved | T081 |

---

## OQ-003: Repository URLs and FlowDokumenty tables

**Status**: Repository resolved. Live `FLOWVISION2` schema read on 2026-11-12 (`INFORMATION_SCHEMA.COLUMNS`, local SQL Server, Windows authentication). Document and department tables resolved. Process tables need a product decision.

**Live schema findings**:
- Present: `DOCUMENT` (15 columns, `DOC_*`, matches the repo DDL), `DEPARTAMENTS` (`DEP_*`), `PRODUCTS` (`PRD_*`), `PRODUCTS_ITEMS`, `PROJECT` (`PROJ_*`), `PROJECT_NAG` (`PROJ_*`), `SNAG` (`SNAG_*`), `TROLLEY_QRCODE` (`TRQR_*`), `USERS`, `REQUEST`, `REQUEST_OPERATIONS`, and the others in the repo scripts.
- Absent: `DOKUMENT`, `DZIAL`, `PROCESY`, `PRODUCT` (singular), `TROLEY_QRCODES`.
- Consequence: FlowDokumenty's SQL (`DOKUMENT`, `DZIAL`, `PROCESY`, `PRODUCT`, `DOK_*`) cannot run against `FLOWVISION2`. Either it points at another database or that module is not in use. This must be confirmed by the product owner.

**Evidence**:
- `git remote -v` in this checkout returns one remote for every folder: `https://github.com/szyurban75/flowvision.git`.
- `FlowDokumenty/` and `ProcessUser/` are tracked folders in that repository (`git ls-files`). No `.gitmodules` file exists, so they are not submodules.
- The spec (Assumptions) and research.md §9 state that FlowDokumenty and ProcessUser are separate repositories. That was wrong for this checkout. The spec text is updated in this change.
- FlowDokumenty's SQL uses `DOKUMENT`, `DZIAL`, `PROCESY`, `PRODUCT` and columns with the `DOK_` prefix. The repository DDL has `DOCUMENT` with `DOC_` columns. `DZIAL`, `PROCESY` and `PRODUCT` (singular) are not created by any script in the repository.
- The FlowDokumenty and ProcessUser `config.cfg` files point at database `FLOWVISION2`.

**Decision**:
1. Repository: `https://github.com/szyurban75/flowvision` holds all three desktop apps, FlowDokumenty and ProcessUser. The platform goes in `platform/` in the same repository (plan.md structure decision stands).
2. Document tables: `DOCUMENT` with `DOC_*` columns (live). T099 is unblocked. Department: `DEPARTAMENTS` (live). Process: the live schema has no process table (`DZIAL`/`PROCESY` absent). **Product decision needed** for T100 (process part):
   - Option A: processes are a new table created by the platform (new migration in the 2.0.x sequence).
   - Option B: processes are stored in an existing table not yet identified (give the name).
   - Option C: the Documents web interface does not manage processes in this release.
   **Decided by product owner (2026-11-12): option C.** Processes are not managed in the Documents web interface in this release. Spec US3, FR-019, and the Key Entities were updated. Documents link to production orders, items, products, projects, and operations through the live `DOC_*` columns. The desktop application keeps managing processes.

---

## OQ-004: Role list and role mapping

**Status**: Resolved. Role codes from code; role-to-interface mapping confirmed by the product owner on 2026-11-11.

**Evidence**: `USERS_ROLE` values used in code (`QueriesLibrary/CQueryLibrary_Users.cs`, `CQueriesLibrary_Reports.cs`, ProcessUser `AnalysisQuery.cs`):

| Code | Label in code | Meaning |
|---|---|---|
| `A` | Administrator | Full administration |
| `B` | Brygadzista | Foreman or shift leader |
| `U` | Uzytkownik | Operator (user) |

No other values appear in the code. The live `USERS` table exists (13 columns); the role counts in the data have not been checked yet.

**Confirmed mapping** (for T049 and T069):

| Web interface | Allowed roles | Notes |
|---|---|---|
| Admin (`platform/web/admin`) | `A` | Production orders, supervision, KPI |
| ShopFloor (`platform/web/shopfloor`) | `U`, `B`, `A` | Scan flow for operators and foremen |
| Documents (`platform/web/documents`) | `A`, `B`, `U` read; `A` write | Viewing for all active users; editing for administrators only |

Users with `USERS_STATUS` other than `A` cannot sign in to any interface.

**Confirmed by product owner (2026-11-11)**: `B` may use the ShopFloor interface, and `A` alone may use the Admin interface.

---

## OQ-005: Password hash algorithm and legacy credentials

**Status**: Confirmed by product owner on 2026-11-11. The current scheme cannot be carried into the platform.

**Evidence**:
- `ProcessAdmin/ZlecLibrary/CUsers.cs` (`CUser_PasswordVerification`) and `ProcessUser/FV_DbObjects/CUser.cs` store and check passwords as `sPassword.GetHashCode().ToString()`, a 32-bit, non-cryptographic number stored as text.
- `string.GetHashCode()` is randomized per process in .NET Core and later. A .NET 10 service cannot reproduce the stored values, so existing credentials cannot be verified by the platform.
- `USERS_PASSWORD` is `varchar(64)` in every script that defines it. A modern salted hash (PBKDF2 in ASP.NET Core format) is about 84 characters, so the column must be widened.
- Empty passwords: `CUser_PasswordVerification` accepts an empty password when the stored value is empty. The platform must not do this.

**Decision**:
1. **New hashes**: `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` (PBKDF2-HMAC-SHA256 with a per-password salt, framework default iteration count). Stored in `USERS_PASSWORD`.
2. **Schema**: `db_mod_FlowVision_2.0.36.sql` widens `USERS_PASSWORD` from `varchar(64)` to `varchar(128)`. The desktop applications still read and write it as text, so they are unaffected. Added to T022 and T023.
3. **Legacy values** (anything that is not in the new format): the platform refuses web sign-in with a clear message ("password reset required"). An administrator sets a new password through an Admin service endpoint, which uses the new hasher. The endpoint is part of T047.
4. **Empty or NULL `USERS_PASSWORD`**: sign-in refused on the web interfaces.
5. **Desktop**: unchanged in this feature. Desktop password changes still write the legacy value, so they will require a reset on the web. Changing the desktop hash is out of scope (desktop security is deferred).

**Confirmed by product owner (2026-11-11)**: web users with a legacy password must get a reset from an administrator the first time they sign in on the web. The alternative is to keep the legacy check in a .NET Framework helper, which is not recommended because it keeps the weak algorithm alive.

---

## OQ-007: `REQUEST.REQ_STATUS` enum

**Status**: Resolved from code.

**Evidence**:
- `SetRequestStatus('F')` (`ZlecLibrary/CZlecenie.cs`) and `CRequest_SetStatus('F')` (ProcessUser `FV_Services/Srv_RequestOperation.cs`, `MainForm.cs`) write closed orders.
- `CRequest_SetStatus('A')` (ProcessUser `MainForm.cs`, `Srv_RequestOperation.cs`) writes active orders.
- Read-only values in reports and queries: `O` (awaiting, `CQueriesLibrary.cs` counts), `I` (import), labelled "Import".
- `Z` appears only in display `CASE` expressions (`Zamknięte`). No code path writes it. Treat it as a legacy alias of `F`.

**Mapping for T081**:

| Value | Label | Written by platform |
|---|---|---|
| `O` | Awaiting (Oczekujące) | No (read only) |
| `A` | Active (Aktywny) | Yes, on first operation start |
| `F` | Closed (Zamknięte) | Yes, when the last operation ends |
| `I` | Import | No (read only) |
| `Z` | Legacy label for closed | Read as `F`; never written |

Any other value: `UNKNOWN`, shown as "Status niepoprawny" in the desktop.

---

## Table names verified against the repository scripts

Checked against `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/*.sql` and the FlowDokumenty and ProcessUser code. Live-database schema confirmed 2026-11-12 (see OQ-003).

| Data model name | Verified name | Source |
|---|---|---|
| `REQUEST` | `REQUEST` | created in 2.0.x scripts |
| `REQUEST_OPERATION` | `REQUEST_OPERATIONS` | created; used in code |
| `TIME_OF_OPERATION` | `TIME_OF_OPERATIONS` (also `TIME_OF_OPERATIONS_ADMIN`) | created; used in code |
| `TIME_OF_OPERATIONS_QRCODES` | `TIME_OF_OPERATIONS_QRCODES` | created in 2.0.35 |
| `TROLEY_REQUEST` | `TROLEY_REQUEST` | created in 2.0.22 and later |
| `TROLEY_REQUEST_OPERATIONS` | `TROLEY_REQUEST_OPERATIONS` | created |
| `TROLLEY_OPERATIONS` | `TROLLEY_OPERATIONS` | created in 2.0.35 |
| `TROLLEY_QRCODES` (data model) | Live: `TROLLEY_QRCODE` exists (`TRQR_*`); `TROLEY_QRCODES` does not. | Use `TROLLEY_QRCODE` (confirmed live) |
| `QRCODE` | `QRCODE` | created |
| `REQUEST_QRCODE` | `REQUEST_QRCODE` | created |
| `COMPONENT` | `COMPONENT`, `COMPONENT_QRCODE` | created |
| `USERS`, `USERS_FUNCTIONS` | same | created |
| `MACHINE` (workstation) | `MACHINE` | created; used in code (106 references) |
| `MACHINE_FAILURE_TYPE`, `MACHINE_FAILURE_EVENT` | same | created in 2.0.35 |
| Defect (SNAG) | `SNAG` (live, `SNAG_*`) and `PROJECT_NAG` (live, `PROJ_*`, project data, not defects) | `SNAG` is the defect table. Confirmed live. |
| `PROJECT` | `PROJECT` | created |
| `PRODUCTS`, `PRODUCTS_ITEMS` | Both live. `PRODUCT` (singular) does not exist. | Use `PRODUCTS` (confirmed live) |
| Department | `DEPARTAMENTS` (created; 61 references). Not `DEPARTMENTS`. | Use `DEPARTAMENTS` |
| Document | `DOCUMENT` (live, `DOC_*` columns). FlowDokumenty's `DOKUMENT` (`DOK_*`) does not exist live. | Use `DOCUMENT` (confirmed live) |
| Process, Department in FlowDokumenty | `DZIAL`, `PROCESY` do not exist live. Department is `DEPARTAMENTS`. | Process: product decision (OQ-003). Department: `DEPARTAMENTS` |
| `ADMIN_OPERATIONS_DIC` | same | created |

## Migration baseline (confirmed)

- Latest script in `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/`: `db_mod_FlowVision_2.0.35.sql`. No 2.0.36 exists.
- The next script is `db_mod_FlowVision_2.0.36.sql`. T021 is resolved.
- `db_mod_FlowVision_2.0.36.sql` also widens `USERS_PASSWORD` to `varchar(128)` (OQ-005).
