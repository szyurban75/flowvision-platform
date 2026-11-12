# ADR-001: Entity Framework Core as the data-access layer between the database and the codebase

**Status**: Accepted on 2026-11-11 (approved by the user; the approver's role is to be confirmed in the table below)
**Date**: 2026-11-11
**Feature**: 008-multiservice-migration
**Supersedes (in part)**: constitution v1.0.0 Principle I (multiservice and web), Principle II (raw ADO.NET, no ORM) for the new platform, Principle VI (zero automated tests) for the new platform, and the Technology Stack table rows for runtime, UI and ORM.
**Related**: constitution v2.0.0 Principle V (EF Core migrations allowed, amended by this feature), [spec.md](../spec.md), [plan.md](../plan.md), [research.md](../research.md).

## Context

FlowVision today is three WinForms applications (ProcessAdmin, ProcessUser, FlowDokumenty) that share one SQL Server database, `FLOWVISION2`. Each application reaches the database through hand-written ADO.NET: `CDBConnDetails` is the only entry point, and SQL text is built in the `FV_DbObjects` and `ZlecLibrary` classes with `SqlParameter` lists.

The migration (see spec.md) splits the system into four backend services and three React web interfaces. The new services must:

- read and write the same tables as the desktop applications during the transition (FR-007);
- use Entity Framework, as required by the product owner (the product owner's request: "ORM - Entity Framework");
- keep SQL injection impossible (FR-005, constitution Principle III);
- detect concurrent edits with a `rowversion` column on every shared table (clarification Q2-B and Q3);
- stay on a supported runtime for the pilot and after (.NET 8 reaches end of support in November 2026).

Principle II of the constitution forbids an ORM for the current major version. The product owner's decision and the migration goals require one for the new platform. The conflict must be decided in writing, not worked around.

## Decision

1. **EF Core is the data-access layer between the database and the codebase for every new service.** No code in a new service talks to SQL Server except through EF Core: `DbContext` classes, LINQ queries, and, only where LINQ cannot express the query, `FromSql` with interpolated parameters. Nothing in the services builds SQL by string concatenation.

2. **Layer rules for the new platform** (the ORM sits in the middle):

   ```text
   React web interface  ->  HTTP API (service)  ->  Application/service layer  ->  EF Core DbContext  ->  SQL Server
   ```

   - Only the data-access project (`Platform.Data`) references EF Core and the SQL Server provider.
   - Application and service code works with DTOs and domain types, not with EF entity types. Entity types never leave the data-access project, so the API contract does not depend on table shape.
   - The HTTP layer does not reference `DbContext` directly.

3. **One `DbContext` per service**, over the tables that service owns or reads. Shared tables are mapped in every context that uses them, with explicit `ToTable` and column mappings to the legacy names. No context maps the whole schema.

4. **Concurrency**: every mapped shared table has a `rowversion` column (`IsRowVersion()` in the mapping). Queries selected to check it (clarification Q3) use the row version in the update; a mismatch raises `DbUpdateConcurrencyException`, which the service maps to HTTP `409` with code `CONCURRENCY`.

5. **Schema**: EF Core migrations are the authoring tool. Each migration is exported with `dotnet ef migrations script --idempotent` into a reviewed `db_mod_FlowVision_2.0.N.sql` script, which is the only artifact applied to a database (constitution Principle V, version 2.0.0). `Database.Migrate()` is never called at run time. Each service checks the schema version at start and refuses to run on a mismatch (FR-006).

6. **Runtime**: .NET 10 LTS and EF Core 10, matching the LTS line.

7. **Desktop applications are unchanged.** They keep raw ADO.NET through `CDBConnDetails` (constitution Principle II, for `.NET Framework 4.7.2`) until each module is retired. The two data-access styles coexist on one database. The ORM decision does not apply to the desktop code.

8. **Testing (supersedes Principle VI for the new platform)**: automated tests are required for the data-access layer and the cost calculation (FR-018), against `FLOWVISION2_TEST` only. Manual acceptance remains required for every user story (constitution Principle VI, unchanged).

## Secrets and database authentication

9. **Primary (site with Active Directory): group Managed Service Account (gMSA).** Each platform service runs as its own gMSA. SQL Server gets a login for that gMSA, and the connection string uses Windows Integrated Security. No password exists to store or rotate.
   - Requires: an AD security group that contains the server, a gMSA per service, and a SQL login for each gMSA with only the permissions the service needs.
   - Dependency: the AD team must create the gMSAs.

10. **Secondary (site without Active Directory): DPAPI-protected secret file.** SQL authentication with a dedicated low-privilege login. The password is encrypted with `ProtectedData` at `LocalMachine` scope and stored in `%ProgramData%\FlowVision\secrets\`, outside the repository. The file ACL allows only the service account and Administrators. The services decrypt it at start and never log the value.
   - This is the only case in which SQL authentication is used in production.

11. **Development machines**: each developer runs a local SQL Server (Express or Developer edition) that trusts the developer's own Windows account, and uses `FLOWVISION2_TEST`. No secret is needed. The local SQL instance does not need to trust AD. The rules in items 9 and 10 apply to production and pilot sites only.

12. **Never stored**: secrets are never placed in the repository, in `appsettings*.json` files committed to Git, or in `config.cfg`. `dotnet user-secrets` may be used on developer machines only.

13. **Desktop applications** keep reading `config.cfg` until they are changed or retired. This is tracked separately and is not part of ADR-001.

**Why**: A gMSA removes the secret entirely, which is the strongest option where AD exists. DPAPI keeps the secret off the repository and out of plain text without new infrastructure, which suits a standalone site.

## Consequences

**Positive**

- Parameterization is built in, so the SQL injection rule (Principle III) is enforced by the framework, not by reviewers alone.
- One place (the `DbContext`) defines how the new platform reads and writes data, which makes concurrency (`rowversion`) and audit fields (FR-011) consistent.
- Entity types are kept out of the API, so the database can change without breaking the React interfaces.
- EF Core is supported on current .NET and has long-term tooling; ADO.NET code in the new services would be written and maintained by hand.

**Negative and risks**

- Two data-access styles exist for the same database until the desktop applications are retired. Developers must know which style applies to which code.
- EF Core can generate inefficient SQL. Each query in the scan flow (User Story 1, FR-013: 2 s) must be checked against a 2-second budget, using a query plan or logged SQL, before the slice is accepted.
- Change tracking can cause surprising behaviour with `rowversion` if entities are reattached. The rule is: the row version comes from the client's `If-Match` value, never from a previously loaded entity.
- Desktop `INSERT`/`SELECT *` compatibility with the new `rowversion` columns must be verified (see research.md §3). This is not an ORM issue but it affects the same database.
- The EF Core learning curve for the team (no existing EF experience is recorded in the repo).

**Neutral**

- Migrations are generated code, so reviewers review the exported SQL script, not the C# migration class alone.

## Alternatives considered

1. **Keep raw ADO.NET in the new services (status quo, Principle II).** Rejected: conflicts with the product owner's ORM requirement. It would also keep SQL hand-written in every new service, which is the main injection risk the constitution is trying to remove.
2. **Dapper (micro-ORM).** Rejected: the product owner asked for Entity Framework. Dapper would also still need hand-written SQL and a separate concurrency mechanism.
3. **EF6 on .NET Framework 4.7.2.** Kept as a fallback only. It would allow sharing code with the desktop unchanged, but has less tooling, no LTS path for the new web hosting, and was rejected as the default for new services (research.md §1).
4. **Shared EF model between the desktop and the services.** Rejected for now: would force the desktop to adopt EF, which Principle II forbids, and expand the scope of the migration. Revisit only if the desktop is retired.

## Open items (must be closed before the first slice)

- **OQ-004**: the full role list (`USERS_ROLE`) affects authorization, not the ORM layer, but it blocks FR-010.
- **OQ-005**: password hash algorithm. Credential verification lives in the Identity service and uses the same `DbContext` layer.
- **OQ-003**: repository URLs for FlowDokumenty and ProcessUser. These do not change the decision but affect where their tables are mapped.
- **Query budget**: the 2-second scan budget (FR-013) must be measured on the site's hardware for the scan query.

## Deferred (desktop, out of scope for this ADR)

- The desktop `config.cfg` files are tracked in git and hold a database password, which is also in history. Branch `002-secure-config-password` (commit `360eb26`) has a DPAPI vault for the desktop, not yet merged. Rotation and history cleanup are needed. Tracked separately; not part of the platform work.

## Approval

| Role | Name | Decision | Date |
|---|---|---|---|
| Product owner (role to confirm) | Approved by the user | Approved | 2026-11-11 |
| Senior developer (constitution amendment procedure) | _(pending)_ | | |

When approved, the constitution's Principles I, II and VI and the Technology Stack table must be amended to reference this ADR in the same PR (constitution Amendment Procedure, step 4).
