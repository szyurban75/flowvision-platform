# Tasks: FlowVision Multiservice Modernization

**Input**: Design documents from `/specs/008-multiservice-migration/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/service-endpoints.md, quickstart.md, adr/ADR-001-orm-data-access-layer.md

**Gate before any task**: ADR-001 is accepted (2026-11-11). The constitution amendments in `adr/ADR-001-constitution-amendments.md` are applied to the working tree (version 3.0.0). The amendment PR must be merged with senior developer approval before Phase 2 is merged to main.

**Tests**: Required for the platform. ADR-001 item 8 and constitution Principle VI (3.0.0) mandate automated tests for the data-access layer, the cost calculation (FR-018, SC-008), React components, and one scan-flow smoke test. Test tasks are therefore not optional for this feature. Manual acceptance is also required for every user story.

**Organization**: Tasks are grouped by user story (priority order from spec.md) so each story can be implemented, tested, and delivered independently.

**Blocked items**: tasks marked `BLOCKED: OQ-00X` cannot be finished until that open question is closed. They are listed so the work is not forgotten.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: different files and no dependency on an incomplete task
- **[Story]**: US1 to US5, from spec.md
- All paths are relative to the repository root. The platform lives under `platform/`.

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: create the solution, folders, and build pipeline.

- [ ] T001 Create `platform/Platform.sln` and the folder skeleton from plan.md (`platform/src/Shared`, `platform/src/Services`, `platform/src/Tools`, `platform/tests`, `platform/web`, `platform/deploy`)
- [ ] T002 Create `platform/global.json` pinning the .NET 10 SDK, and `platform/Directory.Build.props` with `Nullable` and analyzers enabled
- [ ] T003 [P] Create `platform/.editorconfig` with C# naming and formatting rules
- [ ] T004 [P] Create `platform/src/Shared/Platform.Data/Platform.Data.csproj` referencing EF Core 10 and `Microsoft.EntityFrameworkCore.SqlServer`
- [ ] T005 [P] Create `platform/src/Shared/Platform.Auth/Platform.Auth.csproj`
- [ ] T006 [P] Create `platform/src/Shared/Platform.Logging/Platform.Logging.csproj`
- [ ] T007 [P] Create `platform/src/Shared/Platform.SchemaCheck/Platform.SchemaCheck.csproj`
- [ ] T008 [P] Create `platform/src/Shared/Platform.Secrets/Platform.Secrets.csproj`
- [ ] T009 [P] Create `platform/src/Shared/Platform.Hosting/Platform.Hosting.csproj` (shared ASP.NET Core defaults)
- [ ] T010 [P] Create `platform/src/Tools/MigrationRunner/MigrationRunner.csproj`
- [ ] T011 [P] Create service projects `platform/src/Services/Identity/Identity.csproj`, `platform/src/Services/Admin/Admin.csproj`, `platform/src/Services/ShopFloor/ShopFloor.csproj`, `platform/src/Services/Documents/Documents.csproj`
- [ ] T012 [P] Create test projects `platform/tests/Platform.Data.Tests/Platform.Data.Tests.csproj`, `platform/tests/ShopFloor.CostCalc.Tests/ShopFloor.CostCalc.Tests.csproj`, and `platform/tests/Platform.Services.Tests/Platform.Services.Tests.csproj` (xUnit)
- [ ] T013 [P] Scaffold React + TypeScript (Vite) app `platform/web/shopfloor` with `npm create vite@latest`, then pin React 19 in `platform/web/shopfloor/package.json`
- [ ] T014 [P] Scaffold React + TypeScript (Vite) app `platform/web/admin`
- [ ] T015 [P] Scaffold React + TypeScript (Vite) app `platform/web/documents`
- [ ] T016 [P] Add Vitest and TanStack Query to all three web apps (`platform/web/*/package.json`)
- [ ] T017 [P] Add Playwright to `platform/tests/e2e/package.json` and create `platform/tests/e2e/playwright.config.ts`
- [ ] T018 Create CI workflow `.github/workflows/platform-ci.yml` that runs `dotnet build`, `dotnet test`, `npm test`, and `npm run build` for each web app on pull requests
- [ ] T019 [P] Add `platform/.gitignore` entries for `node_modules/`, `bin/`, `obj/`, `dist/`, and any local secret file pattern

**Checkpoint**: solution builds (empty projects) and CI runs on a branch.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: database schema foundation, configuration, secrets, logging, error mapping, sign-in, and health checks. No user story can start before this phase is complete.

**CRITICAL**: no user story work begins until this phase is complete.

### 2a. Schema foundation (Q3: row version on all shared tables)

- [ ] T020 Export the list of all user tables in `FLOWVISION2_TEST` (`SELECT name FROM sys.tables`) into `specs/008-multiservice-migration/schema-tables.txt` and review it with the product owner (shared tables only; exclude system and temp tables)
- [ ] T021 Confirm the next free script number after `db_mod_FlowVision_2.0.35.sql` in `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/` (resolved: `db_mod_FlowVision_2.0.36.sql`, see `open-questions.md`; confirm once more before creating the file)
- [ ] T022 Write `ProcessAdmin/ProcessAdmin/ProcessAdmin/database/db_mod_FlowVision_2.0.36.sql` that adds a `ROWVERSION` column to every table in `schema-tables.txt`, with a header comment containing rollback steps (constitution Principle V)
- [ ] T023 In the same script, create the schema version table (name: `PLATFORM_SCHEMA_VERSION`, columns: `VERSION`, `APPLIED_AT`, `APPLIED_BY`), insert version `2.0.36`, and widen `USERS.USERS_PASSWORD` from `varchar(64)` to `varchar(128)` so a salted hash fits (OQ-005, `open-questions.md`)
- [ ] T024 Verify T022 and T023 on `FLOWVISION2_TEST`: run the desktop `INSERT` and `SELECT *` paths (ProcessAdmin and ProcessUser) manually and record the result in `specs/008-multiservice-migration/checklists/schema-validation.md`
- [ ] T025 [P] Create `platform/tests/Platform.Data.Tests/SchemaTests.cs` that asserts every table in `schema-tables.txt` has a `timestamp` column in `FLOWVISION2_TEST`
- [ ] T026 Create `platform/src/Tools/MigrationRunner/Program.cs` that applies `db_*FlowVision*.sql` scripts in ascending version order, records each applied version in `PLATFORM_SCHEMA_VERSION`, and stops on the first error
- [ ] T027 [P] Create `platform/tests/Platform.Data.Tests/MigrationRunnerTests.cs` that runs the runner against an empty test database and checks the recorded version

### 2b. Data-access foundation (ADR-001)

- [ ] T028 Create `platform/src/Shared/Platform.Data/PlatformDbContext.cs` as the base `DbContext` class with `IsRowVersion()` conventions for mapped shared tables
- [ ] T029 [P] Create `platform/src/Shared/Platform.Data/AuditInterceptor.cs` that fills the user and timestamp audit fields on every write (FR-011)
- [ ] T030 [P] Create `platform/src/Shared/Platform.Data/RowVersionExtensions.cs` that parses the `If-Match` header into a row version and rejects malformed values
- [ ] T031 [P] Create `platform/src/Shared/Platform.Data/ConnectionFactory.cs` that builds the connection string for either a gMSA (Windows Integrated Security) or a DPAPI-protected SQL login, selected by configuration
- [ ] T032 Create `platform/tests/Platform.Data.Tests/RowVersionConflictTests.cs` that verifies a stale write raises `DbUpdateConcurrencyException`

### 2c. Secrets (ADR-001 items 9 to 12)

- [ ] T033 Create `platform/src/Shared/Platform.Secrets/ISecretProvider.cs` with a single method to read the SQL login password
- [ ] T034 Create `platform/src/Shared/Platform.Secrets/DpapiSecretProvider.cs` that decrypts a `LocalMachine`-scope file at `%ProgramData%\FlowVision\secrets\` and never logs the value
- [ ] T035 Create `platform/src/Shared/Platform.Secrets/GmsaConnectionProvider.cs` that returns no password (Windows Integrated Security) for gMSA mode
- [ ] T036 [P] Create `platform/deploy/secrets/Set-DpapiSecret.ps1` that encrypts a password at `LocalMachine` scope and sets the file ACL to the service account and Administrators only
- [ ] T037 [P] Create `platform/tests/Platform.Services.Tests/SecretProviderTests.cs` that checks the DPAPI round trip and that no secret appears in log output

### 2d. Logging and error handling (FR-014, ADR-001)

- [ ] T038 Create `platform/src/Shared/Platform.Logging/FileLoggerSetup.cs` that configures Microsoft.Extensions.Logging with a daily-rolling file sink at a path set in configuration
- [ ] T039 Create `platform/src/Shared/Platform.Logging/ErrorMapper.cs` that maps `DbUpdateConcurrencyException` to HTTP 409 with code `CONCURRENCY`, `SqlException` to 503 with code `SERVICE_UNAVAILABLE`, and anything else to 500 with a plain-language message; technical details go to the log only
- [ ] T040 [P] Create `platform/tests/Platform.Services.Tests/ErrorMapperTests.cs` covering the three mappings and checking that no stack trace reaches the response body

### 2e. Schema version check (FR-006)

- [ ] T041 Create `platform/src/Shared/Platform.SchemaCheck/SchemaVersionChecker.cs` that reads `PLATFORM_SCHEMA_VERSION` and compares it with the version the service expects
- [ ] T042 Wire the schema check into service start-up in `platform/src/Shared/Platform.Hosting/PlatformDefaults.cs` so the service refuses to run on a mismatch
- [ ] T043 [P] Create `platform/tests/Platform.Services.Tests/SchemaVersionCheckerTests.cs` for matching, older, and newer versions

### 2f. Shared hosting and health

- [ ] T044 Create `platform/src/Shared/Platform.Hosting/PlatformDefaults.cs` with the shared ASP.NET Core setup (logging, error mapping, schema check, health endpoint)
- [ ] T045 [P] Add `GET /api/<service>/health` returning status and the expected and found schema versions (contracts/service-endpoints.md, Common) to each service: `platform/src/Services/Identity/Program.cs`, `platform/src/Services/Admin/Program.cs`, `platform/src/Services/ShopFloor/Program.cs`, `platform/src/Services/Documents/Program.cs`

### 2g. Identity and sign-in (FR-009, FR-010, Q1-B)

- [ ] T046 Create `platform/src/Shared/Platform.Auth/UserRepository.cs` that reads accounts from `USERS` through `PlatformDbContext`, with no raw SQL
- [ ] T047 Create `platform/src/Shared/Platform.Auth/IPasswordVerifier.cs` and a `PasswordVerifier.cs` implementation using `PasswordHasher<TUser>` for new hashes. Legacy `USERS_PASSWORD` values (not in the new format) and empty or NULL values are refused with "password reset required". Add an administrator password-reset endpoint in the Admin service for those accounts (OQ-005, confirmed; see `open-questions.md`)
- [ ] T048 Create `platform/src/Services/Identity/Api/SignInEndpoints.cs` with `POST /api/identity/sign-in`, `POST /api/identity/sign-out`, and `GET /api/identity/me` (per interface, Q1-B)
- [ ] T049 Create `platform/src/Shared/Platform.Auth/RoleRequirements.cs` with role-based authorization policies using the role codes `A`, `B`, `U` and the interface mapping in `open-questions.md` (OQ-004, confirmed)
- [ ] T050 [P] Create `platform/web/shopfloor/src/pages/SignInPage.tsx`, `platform/web/admin/src/pages/SignInPage.tsx`, and `platform/web/documents/src/pages/SignInPage.tsx`, each calling its own sign-in
- [ ] T051 [P] Create `platform/tests/Platform.Services.Tests/SignInTests.cs` for a valid sign-in, a wrong password, and a user without the interface role
- [ ] T052 [P] Create `platform/web/shared/errorMessages.ts` (plain-language message mapping for 409, 503, and 500), used by all three web apps

**Checkpoint**: Foundation ready. Schema version check works, the row version is in place, sign-in is wired (password verification blocked until OQ-005), and logging and error mapping are in place. User story work can start.

---

## Phase 3: User Story 1 - Operator registers work from a web terminal (Priority: P1) MVP

**Goal**: an operator scans a trolley or QR code in the ShopFloor web interface, starts and stops work, and the result is saved with the same cost figures as the desktop application.

**Independent Test** (spec.md): scan a known trolley code, start work, stop work, and confirm the saved work record matches the screen. Scan an unknown code and confirm the error and no record change.

### Tests for User Story 1 (required by ADR-001)

- [ ] T053 [P] [US1] Export 100 completed operations and their desktop-calculated cost figures to `platform/tests/ShopFloor.CostCalc.Tests/Golden/operations-golden.csv` from `FLOWVISION2_TEST`
- [ ] T054 [P] [US1] Create `platform/tests/ShopFloor.CostCalc.Tests/WorkCostCalculatorGoldenTests.cs` that asserts each golden row matches within 0.01% (SC-008)
- [ ] T055 [P] [US1] Create `platform/tests/Platform.Data.Tests/CodeResolverTests.cs` for a trolley code, a QR code, and an unknown code
- [ ] T056 [P] [US1] Create `platform/tests/Platform.Services.Tests/WorkRecordServiceTests.cs` covering start, stop, stop without start (rejected), and stop with a stale `If-Match` (409)
- [ ] T057 [P] [US1] Create `platform/tests/e2e/scan-flow.spec.ts` as the Playwright smoke test of User Story 1 (scan, start, stop, error for unknown code)

### Data access and domain for User Story 1

- [ ] T058 [P] [US1] Map `TROLEY_REQUEST` in `platform/src/Services/ShopFloor/Data/Entities/TrolleyRequest.cs` (verify column names against the base scripts)
- [ ] T059 [P] [US1] Map `TROLLEY_OPERATIONS` in `platform/src/Services/ShopFloor/Data/Entities/TrolleyOperation.cs`
- [ ] T060 [P] [US1] Map `TROLLEY_QRCODES` and `QRCODE` in `platform/src/Services/ShopFloor/Data/Entities/QrCodeMapping.cs`
- [ ] T061 [P] [US1] Map `TIME_OF_OPERATIONS_QRCODES` (work records) in `platform/src/Services/ShopFloor/Data/Entities/WorkRecord.cs` (verify against the desktop `CTImeOfOperation` mapping)
- [ ] T062 [P] [US1] Map `REQUEST_OPERATIONS` (verified name) in `platform/src/Services/ShopFloor/Data/Entities/RequestOperation.cs`
- [ ] T063 [US1] Create `platform/src/Services/ShopFloor/Data/ShopFloorDbContext.cs` with the mappings above, `rowversion` on each mapped table, and no raw SQL
- [ ] T064 [US1] Create `platform/src/Services/ShopFloor/Application/CodeResolver.cs` that resolves a scanned code to an order and operation, returning `UNKNOWN_CODE` with no side effects
- [ ] T065 [US1] Create `platform/src/Services/ShopFloor/Application/WorkCostCalculator.cs` as a pure function ported from the desktop logic in `ProcessUser/ProcessUser/ProcessUser/FV_DbObjects/CTImeOfOperation.cs` (verify the calculation rules with the product owner before porting)
- [ ] T066 [US1] Create `platform/src/Services/ShopFloor/Application/WorkRecordService.cs` with `StartAsync` and `StopAsync`; `StopAsync` checks the row version from `If-Match` and rejects a stop without a start
- [ ] T067 [US1] Add the transaction boundary and rollback on any exception in `WorkRecordService.cs` (constitution Principle VII, no partial record, spec User Story 1 scenario 4)

### API for User Story 1

- [ ] T068 [US1] Create `platform/src/Services/ShopFloor/Api/ShopFloorEndpoints.cs` with `GET /api/shopfloor/codes/{code}`, `POST /api/shopfloor/operations/{id}/start`, and `POST /api/shopfloor/work-records/{id}/stop` (contracts/service-endpoints.md)
- [ ] T069 [US1] Apply role policy for operators to the ShopFloor endpoints (depends on T049; ShopFloor allows roles `U`, `B`, `A` per `open-questions.md`)

### Web interface for User Story 1

- [ ] T070 [P] [US1] Create `platform/web/shopfloor/src/api/shopfloorClient.ts` with typed calls for the three endpoints and the `If-Match` header
- [ ] T071 [US1] Create `platform/web/shopfloor/src/pages/ScanPage.tsx` with a scan input that clears on each submit, and a result panel for the order and operation
- [ ] T072 [US1] Create `platform/web/shopfloor/src/components/WorkControls.tsx` with Start and Stop buttons and the good and defective quantity inputs
- [ ] T073 [US1] Show the plain-language error from `platform/web/shared/errorMessages.ts` on `ScanPage.tsx` for unknown codes and service failures (spec User Story 1 scenarios 3 and 4)
- [ ] T074 [P] [US1] Create `platform/web/shopfloor/src/components/ScanPage.test.tsx` (Vitest) for the input clearing and error display

### Verification for User Story 1

- [ ] T075 [US1] Measure the scan result time on the local network against FR-013 (2 s) and SC-001 (30 s for scan, start, stop); record the result in `specs/008-multiservice-migration/checklists/us1-performance.md`
- [ ] T076 [US1] Run `platform/tests/e2e/scan-flow.spec.ts` and the golden tests (T054) and record the results
- [ ] T077 [US1] Manual acceptance of User Story 1 on `FLOWVISION2_TEST`: compare each saved work record with the desktop application (constitution Principle VI)

**Checkpoint**: User Story 1 works end-to-end and is independently testable. This is the MVP.

---

## Phase 4: User Story 2 - Supervisor manages production orders in a web interface (Priority: P1)

**Goal**: create, view, edit, and assign production orders; supervision screen; KPI dashboard. Edits are checked with the row version.

**Independent Test** (spec.md): create an order in the web interface, assign it to a workstation and operator, and confirm it appears on the supervision screen and in the desktop application with identical data.

### Tests for User Story 2 (required by ADR-001)

- [ ] T078 [P] [US2] Create `platform/tests/Platform.Services.Tests/ProductionOrderServiceTests.cs` for create, edit, assign, and a stale edit (409)
- [ ] T079 [P] [US2] Create `platform/tests/e2e/admin-orders.spec.ts` for create, assign, and the concurrency warning when two sessions edit the same order (spec User Story 2 scenario 2)
- [ ] T080 [P] [US2] Create `platform/tests/Platform.Data.Tests/SupervisionQueryTests.cs` checking that active and waiting orders are returned

### Data access for User Story 2

- [ ] T081 [P] [US2] Map `REQUEST` with the `REQ_STATUS` values `O`, `A`, `F`, `I` (`Z` read as `F`, never written; OQ-007 resolved in `open-questions.md`) in `platform/src/Services/Admin/Data/Entities/ProductionOrder.cs`
- [ ] T082 [P] [US2] Map workstation and machine tables (verify names) in `platform/src/Services/Admin/Data/Entities/Workstation.cs`
- [ ] T083 [US2] Create `platform/src/Services/Admin/Data/AdminDbContext.cs` with mappings and `rowversion` on each mapped shared table
- [ ] T084 [US2] Create `platform/src/Services/Admin/Application/ProductionOrderService.cs` with create, edit (uses `If-Match`), and assign; each write records the user and time (FR-011)
- [ ] T085 [US2] Create `platform/src/Services/Admin/Application/SupervisionQuery.cs` returning active and waiting orders
- [ ] T086 [US2] Create `platform/src/Services/Admin/Application/KpiQuery.cs` for the KPI figures in `ProcessAdmin` (verify the desktop KPI rules with the product owner before porting)

### API for User Story 2

- [ ] T087 [US2] Create `platform/src/Services/Admin/Api/ProductionOrderEndpoints.cs` with the list, get, create, edit, and assign endpoints from contracts/service-endpoints.md
- [ ] T088 [US2] Create `platform/src/Services/Admin/Api/SupervisionEndpoints.cs` with `GET /api/admin/supervision` and `GET /api/admin/kpi`
- [ ] T089 [US2] Return the `ETag` header from the get endpoint and require `If-Match` on edit (409 on mismatch)

### Web interface for User Story 2

- [ ] T090 [P] [US2] Create `platform/web/admin/src/api/adminClient.ts` with typed calls and the `ETag` and `If-Match` handling
- [ ] T091 [US2] Create `platform/web/admin/src/pages/OrdersPage.tsx` listing orders with filtering
- [ ] T092 [US2] Create `platform/web/admin/src/pages/OrderEditPage.tsx` with create, edit, and assign; shows the reload message on 409
- [ ] T093 [US2] Create `platform/web/admin/src/pages/SupervisionPage.tsx` polling `GET /api/admin/supervision` every 15 s (User Story 2 scenario 3, 30 s maximum staleness)
- [ ] T094 [P] [US2] Create `platform/web/admin/src/pages/KpiPage.tsx`

### Verification for User Story 2

- [ ] T095 [US2] Run `platform/tests/e2e/admin-orders.spec.ts` and the service tests and record the results
- [ ] T096 [US2] Manual acceptance of User Story 2: an order created in the web interface appears in the desktop application with identical data (constitution Principle VI)

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Document controller manages documents (Priority: P2)

**Goal**: list, create, and update documents in the Documents web interface. Processes are not managed in the web interface in this release (OQ-003, option C).

**Independent Test** (spec.md): create a document in the web interface and confirm the same record in the desktop application.

**Prerequisite**: none blocking. OQ-003 is resolved (document and department tables from the live schema; process decision: option C).

### Tests for User Story 3 (required by ADR-001)

- [ ] T097 [P] [US3] Create `platform/tests/Platform.Services.Tests/DocumentServiceTests.cs` for create, update, and link to a production order or operation
- [ ] T098 [P] [US3] Create `platform/tests/e2e/documents.spec.ts` for create and the cross-check with a desktop read (manual step recorded in the test notes)

### Data access for User Story 3

- [ ] T099 [P] [US3] Map the document table `DOCUMENT` (live columns `DOC_ID`, `DOC_NAZWA`, `DOC_OPIS`, `DOC_PLIK`, `DOC_ITEM_ID`/`_NR`, `DOC_PROD_ID`/`_NR`, `DOC_PROJ_ID`/`_NR`, `DOC_REQ_ID`/`_NR`, `DOC_OPER_ID`/`_NUMBER`, `DOC_REVISION`) in `platform/src/Services/Documents/Data/Entities/Document.cs`. Resolved by the live schema read (OQ-003). FlowDokumenty's `DOKUMENT` does not exist in `FLOWVISION2`; see `open-questions.md`.
- [ ] T100 [P] [US3] Map the department table `DEPARTAMENTS` (live) in `platform/src/Services/Documents/Data/Entities/Department.cs`. No process entity: processes are not managed by the platform in this release (OQ-003, option C)
- [ ] T101 [US3] Create `platform/src/Services/Documents/Data/DocumentsDbContext.cs` with mappings and `rowversion` on each mapped shared table

### Application and API for User Story 3

- [ ] T102 [US3] Create `platform/src/Services/Documents/Application/DocumentService.cs` with list, create, and update (uses `If-Match`), recording user and time
- [ ] T103 [US3] Create `platform/src/Services/Documents/Application/DepartmentService.cs` for read-only department lookups
- [ ] T104 [US3] Create `platform/src/Services/Documents/Api/DocumentEndpoints.cs` with the document endpoints from contracts/service-endpoints.md
- [ ] T105 [US3] Create `platform/src/Services/Documents/Api/DepartmentEndpoints.cs` with the department endpoint (GET only)

### Web interface for User Story 3

- [ ] T106 [P] [US3] Create `platform/web/documents/src/api/documentsClient.ts`
- [ ] T107 [US3] Create `platform/web/documents/src/pages/DocumentsPage.tsx` with list, create, and update
- [ ] T108 [US3] Create `platform/web/documents/src/pages/DepartmentsPage.tsx` for read-only departments (optional: skip if the Documents interface does not need it in the first release)

### Verification for User Story 3

- [ ] T109 [US3] Manual acceptance of User Story 3: documents written in the web interface match what the desktop application shows (constitution Principle VI)

**Checkpoint**: User Stories 1, 2, and 3 work independently.

---

## Phase 6: User Story 4 - Each product ships and updates independently (Priority: P2)

**Goal**: each web interface and backend service can be built, deployed, and rolled back without redeploying the other services (FR-008, SC-004).

**Independent Test** (spec.md): change one screen in the Documents web interface, deploy only that service, and confirm the other services and interfaces are unchanged.

### Deployment units

- [ ] T110 [P] [US4] Create `platform/deploy/windows-services/install-service.ps1` that installs one service as a Windows service running as its gMSA (ADR-001 item 9), with the service name passed as a parameter
- [ ] T111 [P] [US4] Create `platform/deploy/windows-services/uninstall-service.ps1`
- [ ] T112 [P] [US4] Create `platform/deploy/iis/site.config` with the static React builds and the reverse proxy rules for `/api/identity`, `/api/admin`, `/api/shopfloor`, and `/api/documents`, each to its own port
- [ ] T113 [P] [US4] Create `.github/workflows/deploy-shopfloor.yml`, `.github/workflows/deploy-admin.yml`, `.github/workflows/deploy-documents.yml`, and `.github/workflows/deploy-identity.yml`, each triggered only by changes to its own folder (path filters) and run on the site's self-hosted runner
- [ ] T114 [US4] Create `platform/deploy/rollback.ps1` that restores the previous published folder for one service and restarts only that service (must finish within 15 min, spec User Story 4 scenario 2)
- [ ] T115 [US4] Keep the previous two published versions per service in the deploy scripts so rollback has a target

### Verification for User Story 4

- [ ] T116 [US4] Test: deploy only the Documents service on the test server, then confirm `GET /api/admin/health` and `GET /api/shopfloor/health` still return OK and the admin and shopfloor pages still load (SC-004, SC-005)
- [ ] T117 [US4] Test: run `platform/deploy/rollback.ps1` for Documents and time it against the 15-minute limit; record the result in `specs/008-multiservice-migration/checklists/us4-rollback.md`

**Checkpoint**: each product deploys and rolls back on its own.

---

## Phase 7: User Story 5 - Administrator runs the platform on free infrastructure (Priority: P3)

**Goal**: a site administrator installs the pilot from a written guide in under one working day, with no licence cost (SC-006, SC-007).

**Independent Test** (spec.md): follow the installation guide on a clean environment, start all services and web interfaces, sign in, and complete User Story 1 end-to-end.

- [ ] T118 [P] [US5] Create `platform/deploy/sql/Install-SqlExpress.md` with the SQL Server Express 2022 setup steps and the 10 GB limit check (FR-020)
- [ ] T119 [P] [US5] Create `platform/deploy/gmsa/Create-Gmsa.ps1` that creates the gMSA per service and the SQL login for each (ADR-001 item 9). Requires AD team input
- [ ] T120 [P] [US5] Create `platform/deploy/secrets/Install-DpapiFallback.md` for the no-AD fallback (ADR-001 item 10), reusing `Set-DpapiSecret.ps1` (T036)
- [ ] T121 [US5] Write `platform/deploy/INSTALL.md` as the site installation guide covering prerequisites, database, secrets, services, IIS, the self-hosted runner, and first sign-in (FR-017)
- [ ] T122 [US5] Test the guide on a clean VM and record the time in `specs/008-multiservice-migration/checklists/us5-install.md` (SC-006: under one working day)
- [ ] T123 [US5] Run the pilot with the desktop application open on the same database and record any conflicting writes for two weeks (SC-003)

**Checkpoint**: the pilot can be installed from the guide without developer support.

---

## Final Phase: Polish and cross-cutting concerns

**Purpose**: quality checks that affect all stories.

- [ ] T124 [P] Add a CI check that fails the build if any file under `platform/` contains string-concatenated SQL (constitution Principle III), for example `platform/tools/check-sql-concat.ps1`
- [ ] T125 [P] Add a CI check that fails if DDL (`CREATE TABLE`, `ALTER TABLE`, `DROP`) appears in any `.cs` file outside `Migrations/` (constitution Principle V, 3.0.0)
- [ ] T126 [P] Verify the viewport requirement (FR-015) at 1280 x 720 on all three web apps and record in `specs/008-multiservice-migration/checklists/viewport.md`
- [ ] T127 [P] Verify the cost calculation against 100 new operations after all stories are complete (SC-008) and record the result
- [ ] T128 Re-run `specs/008-multiservice-migration/quickstart.md` from a clean checkout and update any step that changed
- [ ] T129 Final manual acceptance against all acceptance scenarios in spec.md, signed off by the product owner (SC-002), recorded in `specs/008-multiservice-migration/checklists/acceptance.md`
- [ ] T130 Update `specs/008-multiservice-migration/contracts/service-endpoints.md` to match the implemented endpoints
- [ ] T131 Confirm the constitution amendment PR (version 3.0.0) is merged with senior developer approval, and that `adr/ADR-001-orm-data-access-layer.md` status is updated to match

---

## Dependencies and execution order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup; blocks every user story
- **User Story 1 (P1)**: starts after Phase 2. This is the MVP.
- **User Story 2 (P1)**: starts after Phase 2. Can run in parallel with US1 if staffed.
- **User Story 3 (P2)**: starts after Phase 2. T099 and T100 are unblocked (process decision: option C).
- **User Story 4 (P2)**: starts after Phase 2. Needs at least one deployable service (US1 or US2).
- **User Story 5 (P3)**: needs US1 complete to test the end-to-end install.
- **Final Phase**: depends on all stories.

### Blocking open questions

Resolutions are recorded in `open-questions.md`:

- **OQ-003** (repositories and tables): repositories resolved; all desktop apps are in `https://github.com/szyurban75/flowvision`. Live `FLOWVISION2` schema read: `DOCUMENT` (`DOC_*`) and `DEPARTAMENTS` exist. `DOKUMENT`, `DZIAL`, `PROCESY`, and `PRODUCT` do not. T099 and T100 can proceed. Process decision (product owner): option C, processes not managed in the web interface in this release.
- **OQ-004** (roles): resolved; interface mapping confirmed by the product owner. T049 and T069 can proceed.
- **OQ-005** (password hash): confirmed (`PasswordHasher<TUser>`, legacy credentials need an admin reset, `USERS_PASSWORD` widened). T047 can proceed.
- **OQ-007** (`REQUEST.REQ_STATUS`): resolved from code; T081 can proceed.

### Within each story

- Tests written first and failing before implementation (ADR-001 item 8)
- Entities before `DbContext`, `DbContext` before services, services before endpoints, endpoints before the web pages

### Parallel opportunities

- Setup tasks marked [P] (T003 to T019) can run together
- Foundational tasks marked [P] inside each sub-group can run together
- Within each story, entity mappings marked [P] and web client tasks marked [P] can run together

---

## Implementation strategy

### MVP first (User Story 1)

1. Complete Phase 1 (Setup)
2. Complete Phase 2 (Foundational): the critical gate
3. Complete Phase 3 (User Story 1)
4. Stop and validate: golden cost tests, scan-flow smoke test, and manual acceptance (T075 to T077)
5. Demo the MVP to the product owner

### Incremental delivery

1. Setup and Foundational → foundation ready
2. US1 → validate → MVP
3. US2 → validate → supervisors can use the web interface
4. US3 → validate → documents
5. US4 → validate → independent deployment works
6. US5 → validate → pilot installable from the guide

---

## Summary

- **Total tasks**: 131
- **Setup**: 19 (T001 to T019)
- **Foundational**: 33 (T020 to T052)
- **User Story 1 (P1, MVP)**: 25 (T053 to T077)
- **User Story 2 (P1)**: 19 (T078 to T096)
- **User Story 3 (P2)**: 13 (T097 to T109)
- **User Story 4 (P2)**: 8 (T110 to T117)
- **User Story 5 (P3)**: 6 (T118 to T123)
- **Polish**: 8 (T124 to T131)
- **Blocked tasks**: none. T047, T049, T069, T081, T099, and T100 are unblocked by the decisions in `open-questions.md`.
- **Parallel opportunities**: marked [P] throughout
- **Independent test criteria**: stated per story in the phase headers
- **Suggested MVP scope**: Phase 1, Phase 2, and Phase 3 (User Story 1)

Note: file names for the mapped tables marked "verify" must be checked against the base database scripts (versions 2.0.1 to 2.0.20) before the mapping tasks start. Table lists in `data-model.md` are the starting point.
