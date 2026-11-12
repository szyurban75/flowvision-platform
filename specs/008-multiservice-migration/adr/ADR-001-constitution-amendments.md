# ADR-001 Draft: Constitution amendments (Principles I, II, VI and the Technology Stack table)

**Status**: Accepted on 2026-11-12, approved by Szymon Urbanczyk (senior developer). Applied as constitution 3.0.0 via PR #11.
**Date**: 2026-11-11
**Target file**: `.specify/memory/constitution.md`
**Proposed version**: 2.0.0 → **3.0.0** (MAJOR: Principles I and II are redefined, and Principle VI is materially changed; Principle V was already amended to 2.0.0).

This draft gives the exact replacement text. The amendment procedure in the constitution applies: open a PR, describe the principle changed, the rationale, and the impact on existing code, and get senior developer approval.

## How to apply

1. Wait for approval of ADR-001 (product owner and senior developer).
2. Replace each block below in `.specify/memory/constitution.md`.
3. Set the version line to `3.0.0`, `Last Amended` to the approval date, and add a line to the Sync Impact Report listing the four changes.
4. Run `/speckit-constitution` to propagate the change to the templates.
5. Check that `plan.md` (Constitution Check) and `research.md` no longer show these principles as "VIOLATED, justified".

---

## Change 1: Principle I (replaces "Layered Desktop Architecture")

### I. Layered Architecture and Service Boundaries

The desktop application ProcessAdmin remains a **Windows-only, single-process MES** that follows a strict four-layer hierarchy. The new platform (the `platform/` folder) is a set of independently deployable services with one React web interface per product, as defined in ADR-001.

Rules for the desktop application (unchanged in substance):
- Code MUST be placed in the correct layer: UI → Service/Domain → Data Access → SQL Server.
- UI layer (WinForms/MetroModernUI) MUST NOT call `CDBConnDetails` directly. All DB access goes through Service or QueriesLibrary classes.
- Service/Domain layer MUST NOT reference UI controls or namespaces.
- The `FV_RESTConnection` project is the only permitted outbound HTTP client for the desktop application; no new REST clients may be added without justification.
- The three external repository dependencies (FlowDokumenty, ProcessUser, FlowVisionGuiTools) MUST be cloned to the expected relative paths defined in `Architecture.md`.

Rules for the platform:
- The platform consists of four backend services (Identity, Admin, ShopFloor, Documents) and three React web interfaces (admin, shopfloor, documents). Each service and each web interface MUST be a separate deployment unit with its own version number and rollback procedure (FR-008).
- Each web interface MUST communicate only with its own backend service(s) and the shared Identity service. A web interface MUST NOT access the database directly (FR-002).
- Service boundaries follow product areas. A cross-service call MUST go through a documented HTTP contract under `specs/*/contracts/`.
- Inside each service, the layers are: HTTP API → application/service layer → data access (`Platform.Data`, see Principle II) → SQL Server. The HTTP layer MUST NOT reference a `DbContext` directly.
- The external REST integration (trolley/RFID) is owned by exactly one platform service (spec, Assumptions). Other services that need its data call that service.
- A service MUST NOT reference another service's data-access project. Shared code lives only in `platform/src/Shared/`.

**Why**: The migration requires independently deployable products (User Story 4). The layering rule is kept inside each service, so the desktop rules and the platform rules are both enforceable.

**Impact on existing code**: None for the desktop application. The new `platform/` folder is outside the existing solution.

---

## Change 2: Principle II (replaces "Raw ADO.NET Data Access")

### II. Data Access: ADO.NET for the Desktop, EF Core for the Platform

Data access follows one rule per runtime. The desktop application uses raw ADO.NET; the platform uses Entity Framework Core as the only layer between its code and the database (ADR-001).

Desktop rules (unchanged, `.NET Framework 4.7.2`):
- `CDBConnDetails` (static class) is the **sole permitted entry point** to the database for the desktop application. Accepted methods: `Connect()`, `Disconnect()`, `Select()`, `ExecCMD()`, `BeginTrans()`, `CommitTrans()`, `RollbackTrans()`.
- All desktop queries MUST follow PATTERN-001 (see `Data-Access-Layer.md`): build a `List<SqlParameter>`, pass it to `CDBConnDetails.Select()` or `CDBConnDetails.ExecCMD()`, and read from the returned `SqlDataReader` or `dataTableContainer`.
- DB object classes (e.g., `TROLEY : FV_DB_Base`) own the SQL for their entity. Service classes (`Srv_*`) are thin wrappers that call DB object methods.
- `FV_DB_Base.db_Connect()` / `db_Disconnect()` MUST bracket every write operation; transactions MUST be opened with `db_BeginTrans()` and closed with `db_CommitTrans()` or `db_RollbackTrans()` on exception.

Platform rules (ADR-001):
- EF Core `DbContext` classes, one per service, are the **only** entry point to the database for platform code. Only the `Platform.Data` project may reference EF Core and the SQL Server provider.
- Application and service code MUST work with DTOs or domain types. EF entity types MUST NOT leave `Platform.Data`.
- Queries MUST use LINQ. Raw SQL is allowed only through `FromSql` with interpolated parameters. String concatenation or string formatting into SQL is forbidden.
- Every mapped shared table has a `rowversion` column. A write selected to check it MUST use the client's `If-Match` value; the entity MUST NOT supply its own row version.
- Schema changes follow constitution Principle V (version 2.0.0): EF Core migrations are exported as reviewed `.sql` scripts; `Database.Migrate()` MUST NOT be called at run time.

Both runtimes:
- SQL injection prevention (Principle III) applies to all code, with no exceptions.
- Platform database authentication follows ADR-001 items 9 to 12: a gMSA with Windows Integrated Security is primary; a DPAPI-protected secret file is the fallback where no Active Directory exists. Secrets never live in the repository or in committed configuration files.

**Why**: The product owner chose Entity Framework as the ORM for the new services. The desktop code is kept on ADO.NET because it runs on `.NET Framework 4.7.2` and changing it is out of scope for the migration.

**Impact on existing code**: No change to the desktop application's data access. The desktop code and the platform share one database, so the rules in Principle V and the `rowversion` rule apply to both.

---

## Change 2b: Principle III (wording for the platform)

Principle III currently says all statements "MUST supply parameters via `List<SqlParameter>` passed to the `CDBConnDetails` API". Taken literally, EF Core would violate it. Replace that bullet with:

- Desktop: all `SELECT`, `INSERT`, `UPDATE`, and `DELETE` statements MUST supply parameters via `List<SqlParameter>` passed to the `CDBConnDetails` API (unchanged). The code example below is the desktop pattern.
- Platform: every query MUST be built with LINQ, or with `FromSql` / `ExecuteSql` using interpolated values. EF Core sends each interpolated value as a `SqlParameter`, so the rule is met without a `CDBConnDetails` call. Concatenated or formatted SQL text is forbidden in the platform, as on the desktop.

**Why**: Without this, the EF Core design in ADR-001 breaks the constitution's own NON-NEGOTIABLE rule as written.

## Change 3: Principle VI (replaces the first paragraph and rules 1 to 4; rule 5, the link to `Testing-Strategy.md`, is kept)

### VI. Validation: Manual for the Desktop, Automated and Manual for the Platform

The desktop application has **zero automated tests**. Every desktop feature MUST be manually validated against its acceptance scenarios before deployment. The platform MUST have automated tests and MUST also be manually validated against its acceptance scenarios before deployment.

Rules:
- All new desktop features MUST be validated by running ProcessAdmin against a test database (`FLOWVISION2_TEST`) and exercising the full acceptance scenario from `spec.md`.
- Platform tests MUST include:
  - data-access tests (`Platform.Data.Tests`) against `FLOWVISION2_TEST`;
  - golden tests for the work-record cost calculation (FR-018, SC-008), with sample operations taken from the desktop application's results;
  - unit tests for React components (Vitest);
  - one end-to-end smoke test of the scan flow (Playwright).
- The platform's automated tests MUST NOT run against production `FLOWVISION2`.
- The `tasks.md` for every feature MUST include at minimum one Polish-phase task for end-to-end manual validation (unchanged).
- `dotnet test` and Playwright are not a substitute for manual validation of visual layout, MetroFramework theming, grid behavior (desktop), or the web interfaces at 1280 x 720 (FR-015). A human MUST verify these.
- Test code is reviewed under the same gates as production code.
- Automated tests MUST NOT target desktop WinForms forms. The platform's React components are the only UI covered by automated tests.

**Why**: The cost calculation (SC-008, within 0.01% for 100 operations) and the scan flow (FR-013) cannot be checked reliably by hand after each change. The desktop application stays manual because it has no test infrastructure today.

**Impact on existing code**: None for the desktop application. The platform adds test projects under `platform/tests/`.

---

## Change 5: Principle V (wording follow-up to 2.0.0)

Principle V currently says scripts "MUST be applied manually to each environment". The platform uses a MigrationRunner (quickstart.md, plan.md). Replace that bullet with:

- Scripts MUST be applied to each environment in ascending version order, either manually or by the platform's MigrationRunner. A database records the last version it applied.

**Why**: Keeps the runner allowed by the plan without weakening the ordering rule.

## Change 4: Technology Stack table (replaces the table)

### Technology Stack (Desktop and Platform)

| Layer | Desktop (ProcessAdmin, ProcessUser, FlowDokumenty) | Platform (`platform/`, ADR-001) |
|---|---|---|
| Language | C# .NET Framework 4.7.2. No upgrade without an explicit decision. | C# on .NET 10 LTS. |
| UI | WinForms + MetroFramework. No web or mobile UI in the desktop scope. | React 19 + TypeScript, built with Vite. One SPA per product. |
| Build tool | MSBuild (Visual Studio). `dotnet build` unsupported for desktop release. | `dotnet` CLI for services; `npm` for web builds. CI on GitHub Actions. |
| Database | MS SQL Server (`FLOWVISION2`), one instance per environment. Shared by desktop and platform. | Same database. SQL Server Express 2022 for the pilot (10 GB limit). |
| ORM | None: raw ADO.NET via `CDBConnDetails`. | Entity Framework Core 10, the only data-access layer (ADR-001). |
| Logging | FvSuperLogger (custom). No third-party logging libraries. | Microsoft.Extensions.Logging with a file sink (built-in; decided in ADR-001). |
| Reporting | RDLC + Microsoft Report Viewer. Existing `.rdlc` files in `Raporty/`. | Out of scope for the first release (FR-019). Migrated after the core areas. |
| Hosting | User workstations, Remote Desktop or shared filesystem (see Security Hardening Checklist). | On-site Windows Server with IIS and Windows services (pilot). Cloud free tiers for demos only. |
| Version control | Git, existing repositories (OQ-003 for two of them). | GitHub, monorepo under `platform/`. Branch per change, review before merge (FR-016). |

**Why**: The table describes two runtimes. Each row has an explicit scope so a reviewer can see which rules apply.

**Impact on existing code**: None for the desktop application.

---

## Change 6: Title and scope

The constitution is titled "FlowVision ProcessAdmin Constitution" and its Governance text refers only to ProcessAdmin. Replace the title line `# FlowVision ProcessAdmin Constitution` with:

```text
# FlowVision Constitution

This constitution governs the FlowVision desktop applications (ProcessAdmin, ProcessUser,
FlowDokumenty) and the platform in `platform/`. Where a principle names a runtime, the rule
applies only to that runtime (see Principles I, II, VI and the Technology Stack table).
```

**Why**: Once the platform is covered, a ProcessAdmin-only title would mislead reviewers.

## Change 7: Code Review Gates

Keep the existing desktop checklist and mark its items "(Desktop)". Add these platform gates:

- [ ] (Platform) Queries go through the service's `DbContext`; no `CDBConnDetails` call and no raw SQL outside `FromSql` / `ExecuteSql` (Principle II)
- [ ] (Platform) Entity types do not leave `Platform.Data` (Principle II)
- [ ] (Platform) Writes that check the row version use the client's `If-Match` value (Principle II)
- [ ] (Platform) No DDL outside `.sql` scripts and EF Core migration classes (Principle V)
- [ ] (Platform) Automated tests are added or updated for data-access and cost-calculation changes (Principle VI)

**Why**: The existing gates refer to `CDBConnDetails`, `FV_DbObjects` and WinForms, so they cannot be applied to platform PRs as written.

## Items to check in the same PR

- **Security Hardening Checklist**: it is written for the desktop application. Add a line for the platform: the platform's database account is a low-privilege SQL login, and the connection string comes from configuration (also in Principle II).
- **Principle V**: amended to 2.0.0 in the working tree. Change 5 above adds the runner wording in the same PR. The version line goes to 3.0.0 with the rest.
- **Principle VII**: `FVServiceException` and `FvSuperLogger` are desktop-specific. A platform note may be added in the same PR: services never swallow an exception, roll back on error, log details, and show plain-language messages (FR-014).
- **Compliance review text** (Governance): the Complexity Tracking table in `plan.md` should be cleared once the amendment is in force.
- **Open questions**: OQ-003, OQ-004, OQ-005 remain open and are tracked in `OPEN-QUESTIONS.md`.
- **Secrets (decided)**: the platform uses a gMSA as primary and a DPAPI-protected file as fallback, both described in ADR-001 items 9 to 12. The Security Hardening change in Change 8 applies in the same PR.
- **Desktop credentials (open)**: the desktop `config.cfg` still holds database credentials in plain text. This is a separate backlog item; the constitution text for the desktop is unchanged by this PR.

## Change 8: Security Hardening Checklist (replaces the first bullet)

Replace the first bullet, "SQL Server uses SQL authentication (not Windows auth in production) with a dedicated low-privilege service account.", with:

- Production platform services use a gMSA with Windows Integrated Security (primary). Where no Active Directory exists, SQL authentication with a dedicated low-privilege login is used, and its password is stored only as a DPAPI-protected secret (secondary). The desktop application keeps the existing rule until it is changed.

**Why**: The current bullet forbids Windows authentication in production, which blocks the primary option the site can use. Developer machines are covered by ADR-001 item 11 and are not production.
