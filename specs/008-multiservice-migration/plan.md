# Implementation Plan: FlowVision Multiservice Modernization

**Branch**: `008-multiservice-migration` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-multiservice-migration/spec.md`

**Status**: DRAFT. The constitution gate is OPEN. Implementation is blocked until ADR-001 (architecture decision record) is approved. See Constitution Check.

## Summary

Split the three WinForms applications (ProcessAdmin, ProcessUser, FlowDokumenty) into four backend services (Identity, Admin, ShopFloor, Documents), each with its own React web interface, while the desktop applications keep running on the same `FLOWVISION2` SQL Server database during the transition.

Technical approach (details in [research.md](research.md)):

- **Backend**: .NET 10 LTS services with Entity Framework Core. Schema changes stay in versioned SQL scripts, not EF migrations, because constitution Principle V forbids DDL in application code.
- **Frontend**: three React + TypeScript single-page apps built with Vite, one per product.
- **Data**: SQL Server only. Every shared table gets a `rowversion` column (clarification Q3). Each web query chooses whether to check it.
- **Auth**: a separate sign-in per web interface against the existing `USERS` table (clarification Q1).
- **Hosting (pilot)**: the site's own Windows Server with IIS, Windows-service-hosted APIs, and SQL Server Express 2022 (10 GB limit). This is free and works without internet. Cloud free tiers are for demos only.
- **Source control and CI**: GitHub free plan, Actions with a self-hosted runner on the site server (outbound-only, no inbound port).
- **Build order**: the shop-floor scan flow (User Story 1) first, as a vertical slice across all layers.

## Technical Context

**Language/Version**: C# 14 on .NET 10 LTS (backend); TypeScript 5.x on React 19 (web). Desktop apps unchanged on C# .NET Framework 4.7.2.
**Primary Dependencies**: ASP.NET Core minimal APIs, Entity Framework Core 10 with the SQL Server provider, Microsoft.Extensions.Logging; React 19, Vite, React Router, TanStack Query. Final list confirmed in ADR-001.
**Storage**: Microsoft SQL Server 2016+ (pilot: SQL Server Express 2022, 10 GB per database). Database `FLOWVISION2`; test database `FLOWVISION2_TEST`.
**Testing**: xUnit for service and data-access tests against `FLOWVISION2_TEST` (the cost calculation (FR-018) uses golden data taken from the desktop application); Vitest for React units; Playwright for one end-to-end smoke test of the scan flow; manual acceptance per constitution Principle VI.
**Target Platform**: Windows Server 2022 (or later) on the site LAN with IIS 10; browsers: current Chrome and Edge on Windows 10/11 (FR-015).
**Project Type**: multi-service web platform (3 React SPAs, 4 backend services) with a shared database and unchanged WinForms desktop applications during the transition.
**Performance Goals**: scan result within 2 s on the LAN (FR-013); supervision screen data no older than 30 s (User Story 2, scenario 3), met by 15 s polling; work-record save within 1 s at p95 on the LAN.
**Constraints**: works without internet access (Assumptions, shop-floor reliability); no paid licence for the pilot (FR-020, SC-007); no SQL string concatenation (FR-005); DDL only in versioned scripts (constitution V); each service refuses to start on a schema version mismatch (FR-006).
**Scale/Scope**: about 200 scan-level operations per shift per site (estimate, to be confirmed with the site); a few dozen named users per site; 30+ shared tables. The spec does not give load figures, so this is an assumption to validate in the pilot.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The gate is evaluated against `.specify/memory/constitution.md` (version 1.0.0). Result: **OPEN**. Four principles and the technology-stack table conflict with this feature. Each conflict is justified in Complexity Tracking below and must be closed by ADR-001 before implementation starts.

| Principle | Status | Notes |
|---|---|---|
| I. Layered Desktop Architecture (no microservices or web without an ADR) | **VIOLATED, justified** | Requires ADR-001, which supersedes Principle I. Spec Assumptions require this. |
| II. Raw ADO.NET (no ORM) | **VIOLATED, justified** | EF Core in the new services. ADR-001 supersedes Principle II. Desktop code keeps ADO.NET. |
| III. SQL Injection Prevention (NON-NEGOTIABLE) | PASS with conditions | EF Core LINQ is parameterized. Raw SQL only via `FromSql` with interpolation; a review gate checks for concatenation. |
| IV. MetroModernUI Visual Consistency | N/A to new web code | Applies to WinForms only. The web interfaces need their own style guide (ADR-001 must record that). |
| V. Versioned SQL Migrations | PASS (amended to 2.0.0) | Principle V now allows EF Core migrations as authoring tool. Their exported `--idempotent` scripts are applied by the runner, and `Database.Migrate()` is forbidden. Baseline updated to 2.0.35. |
| VI. Manual Validation Required (zero automated tests) | **VIOLATED, justified** | The spec adds automated tests for the DAL, cost calculation and scan flow. Changing the testing strategy is an ADR-001 item. Manual acceptance stays. |
| VII. Structured Error Handling | PASS with adaptation | `FVServiceException` is a WinForms-era pattern. Services will use the same discipline (no empty catches, rollback on error, log details, plain-language messages to users, FR-014). |
| Technology Stack table (.NET 4.7.2, WinForms, no web UI) | **VIOLATED, justified** | The new services and web interfaces are out of this table's scope. The desktop stack is unchanged. ADR-001 must update the table. |
| Security Hardening Checklist | PASS with open items | Requires OQ-004 (roles) and OQ-005 (password hash) before the sign-in feature (FR-009) can be built. |

**Gate decision**: The plan can proceed to design artifacts (done in this document set). No code is written until ADR-001 is approved. The gate is re-checked after ADR-001 is merged.

## Project Structure

### Documentation (this feature)

```text
specs/008-multiservice-migration/
├── spec.md              # Feature specification (clarified 2026-11-11)
├── plan.md              # This file
├── research.md          # Phase 0: decisions and alternatives
├── data-model.md        # Phase 1: entities mapped to existing tables, rowversion rule
├── quickstart.md        # Phase 1: local and pilot setup (planned steps)
├── contracts/
│   └── service-endpoints.md   # Phase 1: HTTP contract of each service (draft)
├── checklists/
│   └── requirements.md
└── tasks.md             # Created by /speckit-tasks, not by this command
```

### Source Code (repository root)

The new platform lives in a `platform/` folder of this repository (monorepo). The three desktop products keep their current folders unchanged. Migration scripts stay in the one existing location, `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/`, so that all products share one migration sequence (constitution Principle V).

```text
platform/
├── Platform.sln
├── src/
│   ├── Shared/
│   │   ├── Platform.Data/          # EF Core DbContext(s), entity mappings, rowversion handling
│   │   ├── Platform.Auth/          # Sign-in against USERS, password verification, role checks
│   │   ├── Platform.Logging/       # File logging, plain-language error mapping
│   │   └── Platform.SchemaCheck/   # Reads schema version, refuses start on mismatch (FR-006)
│   ├── Services/
│   │   ├── Identity/               # Sign-in per web interface, roles, configuration (FR-003)
│   │   ├── Admin/                  # Production orders, supervision, KPI (ProcessAdmin)
│   │   ├── ShopFloor/              # Scan, start/stop work, work records, cost calc (ProcessUser)
│   │   └── Documents/              # Documents, departments (FlowDokumenty); processes are not managed in this release
│   └── Tools/
│       └── MigrationRunner/        # Applies db_*FlowVision*.sql in version order (one runner only)
├── web/
│   ├── admin/                      # React SPA for ProcessAdmin
│   ├── shopfloor/                  # React SPA for ProcessUser
│   └── documents/                  # React SPA for FlowDokumenty
├── tests/
│   ├── Platform.Data.Tests/        # DAL tests against FLOWVISION2_TEST
│   ├── ShopFloor.CostCalc.Tests/   # FR-018 golden-data tests
│   └── e2e/                        # Playwright scan-flow smoke test
└── deploy/
    ├── iis/                        # Site bindings, reverse proxy rules
    └── windows-services/           # Service install scripts

.github/workflows/                  # Build, test, deploy (self-hosted runner on site)
```

**Structure Decision**: Monorepo under `platform/`. Reasons: one migration sequence and one schema-version check across all services; one pipeline; one team setup for a small group. The two external repositories (FlowDokumenty and ProcessUser, OQ-003) remain as they are until ADR-001 decides their future. If the team later prefers one repository per product, the `src/Services/*` and `web/*` folders can be split without changing the code.

## Phases

- **Phase 0 (done)**: [research.md](research.md). Decisions for runtime, ORM, frontend, hosting, repository layout, CI, testing, auth, concurrency, logging.
- **Gate (blocking)**: ADR-001 written and approved. Covers: supersede Principles I, II, VI and the technology-stack table; runtime and ORM; hosting; logging; testing strategy; open questions OQ-003, OQ-004, OQ-005.
- **Phase 1 (done, draft)**: [data-model.md](data-model.md), [contracts/service-endpoints.md](contracts/service-endpoints.md), [quickstart.md](quickstart.md).
- **Agent context**: no `CLAUDE.md` exists at the repository root, so there is no `<!-- SPECKIT START -->` block to update. Not created by this command; create it if the team wants one.
- **Next**: `/speckit-tasks` after ADR-001 is approved. The first slice is User Story 1 (shop-floor scan flow).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Principle I: multiple services and web deployment | The spec requires three independently deployable web products and services (FR-001, FR-003, FR-008, User Story 4). | A single WinForms app cannot meet independent deployment or the React web requirement. Keeping one monolith would not start the migration. |
| Principle II: EF Core instead of raw ADO.NET | The product owner chose Entity Framework as the ORM. The new services need LINQ-based data access and change tracking for concurrency. | Raw ADO.NET in the new services would give no tooling benefit and would duplicate the desktop DAL. Desktop keeps raw ADO.NET; only new code uses EF Core. |
| Principle VI: automated tests added | FR-018 and SC-008 (cost figures within 0.01%) cannot be verified manually for 100 operations; the scan flow must be checked after every change. | Manual testing alone cannot prove the cost calculation matches the desktop application across many samples. |
| Technology-stack table: .NET 10 LTS, React | Required by the product owner (React, MS SQL, EF, GitHub). Runtime choice is a design decision recorded in research.md. | EF6 on .NET Framework 4.7.2 (kept as fallback) would keep the old runtime and offers less tooling. Its use in new services is only justified if code sharing with the desktop becomes necessary. |
| Principle VII adaptation | The services have no WinForms `MessageBox`; users see plain-language web errors. | Copying the WinForms form-level handler would not apply to HTTP APIs. The discipline (no swallowed errors, rollback, logs, plain text) is kept. |
