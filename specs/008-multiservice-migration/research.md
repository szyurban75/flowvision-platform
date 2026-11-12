# Research: FlowVision Multiservice Modernization

**Feature**: 008-multiservice-migration | **Date**: 2026-11-11

Each decision lists what was chosen, why, and what else was considered. Items marked **Blocking** must be closed in ADR-001 before implementation.

## 1. Backend runtime (Blocking)

- **Decision**: .NET 10 LTS for the new services.
- **Rationale**: .NET 10 is a long-term-support release, supported until November 2028. .NET 8 reaches end of support in November 2026, so it is not a safe choice for a new system. .NET 10 supports EF Core 10 and ASP.NET Core minimal APIs.
- **Alternatives considered**:
  - EF6 on .NET Framework 4.7.2 (the spec's fallback). Keeps the desktop runtime, lets code be shared unchanged, but has less tooling and no LTS path for new web hosting. Kept as a fallback if shared code with the desktop becomes necessary.
  - .NET 8. Rejected: end of support in November 2026.

## 2. ORM and schema management (Blocking)

- **Decision**: EF Core for data access. EF Core migrations are the authoring tool for schema changes in the platform services. Each migration is exported with `dotnet ef migrations script --idempotent` into a reviewed `db_mod_FlowVision_2.0.N.sql` script, and that script is what is applied. Applications never call `Database.Migrate()`.
- **Rationale**: Product owner request, recorded as an amendment to constitution Principle V (version 2.0.0). Generating the scripts keeps one migration sequence shared with the desktop applications, and the schema-version check (FR-006) still works with that sequence. Reviewing the exported SQL keeps the manual-review step.
- **Dependency**: Principle II still forbids an ORM. EF Core cannot be used for data access until ADR-001 supersedes Principle II.
- **Rules**:
  - Read and write through LINQ. Raw SQL only via `FromSql` with interpolation, never string concatenation (FR-005, constitution Principle III).
  - Map to existing tables; do not rename them. Keep the desktop schema as the source of truth.
  - Use `ToTable` and explicit column mappings to match the legacy naming (e.g., `TROLEY_REQUEST`).
- **Alternatives considered**: EF Core Migrations (rejected: DDL in code). Dapper (rejected: the spec requires an ORM).

## 3. Concurrency control (Decided by clarification Q2-B and Q3)

- **Decision**: Add a `rowversion` column to every shared table. Web services check it on save for the queries the implementation selects. Desktop saves are not checked during the transition (accepted limitation, spec edge case).
- **Rationale**: SQL Server fills `rowversion` automatically on every change, so no triggers are needed. The check lets two web users detect each other's changes (FR-012).
- **Risk to verify**: Adding a `rowversion` column to every table, including desktop-only tables, is a broader change than the Option B shown in the clarification. Test on `FLOWVISION2_TEST`, and confirm that the desktop application's `INSERT` statements and `SELECT *` bindings still work.
- **Alternatives considered**: Row-version only on web-written tables (Q2-B's original scope, expanded by Q3). Comparing original values (rejected: no protection against races).

## 4. Frontend

- **Decision**: One React 19 + TypeScript SPA per product, built with Vite. React Router for navigation, TanStack Query for server state and 15-second polling on the supervision screen.
- **Rationale**: Matches the product owner's React mandate. Vite produces static files that IIS serves without a Node runtime on the server. TanStack Query handles the 30-second freshness rule without custom caching code.
- **Alternatives considered**: Next.js (rejected: server rendering is not needed on an intranet; adds a Node runtime). Create React App (rejected: deprecated).
- **Open**: component library and visual style. Constitution Principle IV does not apply to web code; ADR-001 must choose a style guide.

## 5. Authentication (clarification Q1-B)

- **Decision**: Each web interface has its own sign-in against the existing `USERS` table. No single sign-on. The Identity service handles sign-in and role checks for all three.
- **Rationale**: Confirmed by the product owner (Q1 answer B). Keeps the existing accounts and roles.
- **Blocking**: Password hash algorithm (OQ-005) and the full role list (OQ-004). FR-009 and FR-010 cannot be implemented until both are resolved. Existing `USERS_PASSWORD` values must not be re-hashed silently; the migration path for old credentials is part of OQ-005.
- **Alternatives considered**: Single sign-on through the Identity service (Q1 option A, not chosen). Windows authentication (rejected: the desktop uses `USERS`).

## 6. Hosting (pilot)

- **Decision**: On-site Windows Server 2022 (or the existing server) with IIS 10 serving the three React builds and reverse-proxying `/api/*` to four .NET services running as Windows services (Kestrel). SQL Server Express 2022 on the same server or the site's existing SQL Server.
- **Rationale**: No licence cost beyond the existing server (FR-020, SC-007). Works offline on the shop-floor LAN (Assumptions). SQL Express is free with a 10 GB per-database limit, well above the current backup sizes.
- **Cloud alternative (demo only)**: Azure SQL free offer, Azure App Service free tier, Azure Static Web Apps free tier. Rejected for production pilot: usage limits, no SLA, and the scan flow fails without internet.
- **Risk**: The 10 GB Express limit. Monitor database size during the pilot; the `FLOWVISION2` backups are small today, so this is low risk.
- **Blocking**: Confirm the site will host the platform (ADR-001).

## 7. Source control and CI/CD

- **Decision**: GitHub free plan, one monorepo under `platform/` (this repository). GitHub Actions for build and test. Deployment by a self-hosted runner installed on the site server.
- **Rationale**: Self-hosted runners do not consume the free hosted minutes and do not need an inbound port: the runner polls GitHub over outbound HTTPS, which fits the constitution's rule that no inbound port is opened (Security Hardening Checklist).
- **Branching**: one branch per change, review before merge (FR-016).
- **Alternatives considered**: One repository per product (kept as a later option; see plan.md). Hosted runners for deployment (rejected: cannot reach the site network).

## 8. Repository layout

- **Decision**: Monorepo under `platform/` in this repository. Desktop folders unchanged. Migration scripts stay in `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/`.
- **Rationale**: One migration sequence and one schema-version check; one pipeline. The FlowDokumenty and ProcessUser repositories (OQ-003) are not moved in this feature.

## 9. Testing (requires ADR-001)

- **Decision**:
  - xUnit tests for the DAL against `FLOWVISION2_TEST`, never production (constitution Principle VI).
  - Cost calculation (FR-018, SC-008): golden tests with 100 sample operations taken from the desktop application's results.
  - Vitest for React components; Playwright smoke test for the scan flow (User Story 1 independent test).
  - Manual acceptance per user story, as constitution Principle VI requires.
- **Rationale**: The spec's success criteria need measurable checks that manual testing cannot repeat reliably.
- **Alternatives considered**: Manual only (rejected: cannot verify FR-018 across samples).

## 10. Logging and error handling

- **Decision**: Microsoft.Extensions.Logging with a file sink, rolling daily, path configured per site. Users see plain-language messages; technical details go to the log (FR-014). Services roll back the transaction on any exception and never swallow one.
- **Blocking (ADR-001)**: constitution says "no third-party logging libraries" for ProcessAdmin. The new services need an ADR decision on whether the built-in logger plus a file sink is enough.

## Open items carried into ADR-001

- OQ-003: URLs of the FlowDokumenty and ProcessUser repositories.
- OQ-004: full role list (`USERS_ROLE`).
- OQ-005: password hash algorithm and migration of existing credentials.
- Constitution baseline: the scripts go up to 2.0.35, but the constitution says 2.0.34.
- Q3 risk: `rowversion` on every table; verify desktop `INSERT` and `SELECT *` compatibility.
