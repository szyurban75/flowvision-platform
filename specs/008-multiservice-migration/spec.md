# Feature Specification: FlowVision Multiservice Modernization

**Feature Branch**: `008-multiservice-migration`  
**Created**: 2026-11-10  
**Status**: Draft  
**Input**: User description: "Read the repo. It consists of 3 projects: FlowDokumenty, ProcessAdmin, ProcessUser. Create a plan to implement it as a multiservice application with independent WebUI written in React. Database engine: MS SQL Server. ORM: Entity Framework. Suggest first steps such as choosing the platform where it can be deployed, preferably free of charge. Version control: GitHub."

## Clarifications

### Session 2026-11-11

- Q: How should users sign in across the three web interfaces? → A: B - A separate sign-in in each web interface, using the same `USERS` accounts.
- Q: How should concurrent edits be detected on shared tables? → A: B - A row-version column is added only to the shared tables that the web services write. The web services check it on save. Desktop saves are not checked, so a desktop save can overwrite a web change; this is an accepted limitation during the transition.
- Q: Which shared tables get the row-version column? → A: All shared tables. Which queries check the version is selected during implementation.

## Current State (context for planners)

FlowVision today is three Windows desktop (WinForms, .NET Framework 4.7.2) applications that share one SQL Server database (`FLOWVISION2`) and several class libraries:

- **ProcessAdmin**: Manufacturing Execution System for production managers and supervisors (production orders, workstations, supervision, KPIs, reports, RCP time-clock sessions).
- **ProcessUser**: Shop-floor operator application (trolley and QR/barcode scanning, start/stop of operations, defect/SNAG recording).
- **FlowDokumenty**: Document and process-definition management (document lists, document management, process and department definitions).

Shared libraries include the database connection wrapper, the logger, the REST client for external integrations, and GUI tools. The current architecture rule forbids microservices and web deployment without an architectural decision record (see the ProcessAdmin constitution, Principle I). This feature requires that decision to be taken first (see Assumptions).

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Operator Registers Work from a Web Terminal (Priority: P1)

An operator at a shop-floor workstation opens the ProcessUser web interface in a browser, scans a trolley or QR/barcode code, and the system shows the matching production order and operation. The operator starts or stops work on it, and the result is saved and confirmed on screen. Unknown codes produce a clear error and leave no partial state.

**Why this priority**: Scanning is the highest-frequency operator action. If it is unreliable, shop-floor production stops, so it must be the first slice delivered end-to-end.

**Independent Test**: On a workstation with a browser and a scanner, scan a known trolley code, start work, stop work, and confirm the saved time record in the database matches the on-screen result. Scan an unknown code and confirm the error appears and no record changes.

**Acceptance Scenarios**:

1. **Given** a valid trolley code is scanned and the operator is signed in, **When** the operator starts the operation, **Then** the operation shows as in progress and a work record exists with the operator, operation and start time.
2. **Given** a running operation, **When** the operator stops it, **Then** the elapsed time and the good and defective quantities are saved, and the cost figures match those produced by the current desktop application for the same input.
3. **Given** an unknown code is scanned, **When** the system cannot match it, **Then** an error message is shown, and no database record is created or changed.
4. **Given** the database is unavailable during a save, **When** the operator submits the action, **Then** the operator sees an error, the scan input is cleared, and no partial work record remains.

---

### User Story 2 - Supervisor Manages Production Orders in a Web Interface (Priority: P1)

A production manager or shop-floor supervisor uses the ProcessAdmin web interface to view, create, edit and assign production orders, to see active and waiting orders on a supervision screen, and to view the KPI dashboard.

**Why this priority**: Production planning is the core business value of the system. The admin web interface must reach parity for these everyday functions before the desktop application can be retired.

**Independent Test**: Create a production order in the web interface, assign it to a workstation and an operator, and confirm it appears on the supervision screen and in the desktop application with identical data.

**Acceptance Scenarios**:

1. **Given** a signed-in supervisor, **When** they create a production order with required fields, **Then** the order is saved and visible in both the web interface and the desktop application.
2. **Given** two users editing the same production order at the same time, **When** the second user saves after the first, **Then** the second user is warned that the data changed and no silent overwrite occurs.
3. **Given** a supervisor opens the supervision screen, **When** orders change in the database, **Then** the screen shows the current state after a refresh, with no stale data older than 30 seconds.

---

### User Story 3 - Document Controller Manages Documents in a Web Interface (Priority: P2)

A document controller uses the FlowDokumenty web interface to list, create, update and organise documents. Process definitions are not managed in the web interface in this release (decision OQ-003, option C); they stay in the desktop application.

**Why this priority**: Important for quality and compliance, but used less often than shop-floor and planning flows, and it has fewer dependencies on other modules.

**Independent Test**: Create a document in the web interface, then open the same record in the existing desktop application and confirm the data matches.

**Acceptance Scenarios**:

1. **Given** a document controller, **When** they register a document and link it to a production order, item, product, project or operation, **Then** the document appears in the document list with those links.

---

### User Story 4 - Each Product Ships and Updates Independently (Priority: P2)

A developer or IT administrator can build, deploy and roll back each of the three web interfaces and its backend service without redeploying the other two.

**Why this priority**: Independent deployment is the main reason for splitting the system into services. Without it the migration adds complexity without benefit.

**Independent Test**: Change the text of one screen in the FlowDokumenty web interface, deploy only that service, and confirm the ProcessAdmin and ProcessUser services and interfaces are unchanged and still working.

**Acceptance Scenarios**:

1. **Given** a change to one service, **When** only that service is deployed, **Then** the other services keep running without restart or errors.
2. **Given** a failed deployment, **When** the administrator rolls back that service, **Then** it returns to the previous version within 15 minutes.

---

### User Story 5 - Administrator Runs the Platform on Free or Low-Cost Infrastructure (Priority: P3)

An IT administrator or the product owner can run a pilot of the new platform on infrastructure that has no licence cost (see Assumptions for the hosting options), with a documented setup and a single database that the desktop application can still use during the transition.

**Why this priority**: Required to start the pilot at zero cost, but the choice can be revised later without changing the functional scope.

**Independent Test**: Follow the installation guide on a clean environment, start all services and web interfaces, sign in, and complete User Story 1 end-to-end.

**Acceptance Scenarios**:

1. **Given** a clean environment that meets the documented minimum requirements, **When** the installation guide is followed, **Then** all services and interfaces start and are reachable within one working day.
2. **Given** the pilot runs, **When** the desktop application is used at the same time, **Then** both systems read and write the same records without conflicting data.

---

### Edge Cases

- What happens when the database is reachable but one backend service is down? Only the features of that service fail; the other interfaces keep working and show a clear "service unavailable" message.
- How does the system handle a scan on a workstation that loses network connectivity mid-action? The action is rejected with an error and no partial record is stored; the operator retries when the connection returns.
- What happens when the desktop application and the web interface change the same operation at the same time? Optimistic concurrency applies to web saves only: the second web save is rejected with a conflict message and the user is asked to reload. A desktop save is not checked and can overwrite a web change during the transition (accepted limitation, see Clarifications).
- What happens when the same database schema change is applied by more than one service? Only one migration runner applies changes; the others verify the schema version and start only if it matches.
- How does the system handle sign-in when the desktop application and the web interface both authenticate users? Both use the same user records in `USERS`; a user's permissions are the same in both systems.
- What happens to report and export output (PDF, Excel, print) that exists in the desktop application? Each export that is in scope has a web equivalent with the same columns; exports not yet migrated remain available in the desktop application until they are.
- What happens to the outbound integration with the external REST system (trolley/RFID)? It is available to the services that need it and its failures are logged without blocking the operator's action.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide three independent web interfaces: one for ProcessAdmin, one for ProcessUser and one for FlowDokumenty.
- **FR-002**: Each web interface MUST communicate only with its own backend service(s) and the shared service(s) it needs, never directly with the database.
- **FR-003**: The backend MUST be split into separately deployable services, grouped by product area (administration, shop floor, documents) plus a shared identity and configuration service.
- **FR-004**: All persistent data MUST be stored in Microsoft SQL Server, and all data access MUST go through a single data-access layer per service.
- **FR-005**: Each service MUST use the approved ORM for data access; no SQL statement may be built by concatenating user input.
- **FR-006**: The database schema MUST be changed only through versioned migration scripts, applied in a defined order, and each service MUST refuse to start if the schema version does not match what it expects.
- **FR-007**: During the transition, the new services and the existing desktop applications MUST be able to use the same database and the same records without a data copy.
- **FR-008**: Each web interface and service MUST have its own deployment unit, version number and rollback procedure.
- **FR-009**: Users MUST sign in separately to each web interface, with no shared session between the web interfaces. All web interfaces and the desktop application MUST use the same user accounts (`USERS`) and the same roles.
- **FR-010**: Access to each function MUST be restricted by role, and users without the required role MUST NOT see the function in the interface or be able to call it through the service.
- **FR-011**: Every write action MUST be stored with the user, date and time, so changes can be traced.
- **FR-012**: Every shared table MUST have a row-version column. Concurrent web saves to the same record MUST be detected for the queries that the implementation selects to check the version, and the second web save MUST be rejected with a message asking the user to reload. Desktop saves are not checked by this requirement during the transition.
- **FR-013**: The shop-floor scan flow MUST show the result of a scan within 2 seconds on the local network.
- **FR-014**: Errors MUST be shown to users in plain language, and technical details MUST be written to a log that administrators can read.
- **FR-015**: The web interfaces MUST work in the current versions of Chrome and Edge on Windows 10 and Windows 11 and MUST be usable at a 1280 x 720 screen resolution.
- **FR-016**: The source code for all services and interfaces MUST be stored in GitHub, with a branch per change and review before merge.
- **FR-017**: Each service MUST have an installation and configuration guide that a site administrator can follow without developer support.
- **FR-018**: The cost calculations for work records (elapsed time, work cost, distribution across good and defective output) MUST produce the same results as the current desktop application for the same input.
- **FR-019**: The feature set in scope for the first release is: operator scanning and work registration (User Story 1), production order management and supervision (User Story 2), and document management (User Story 3). Other desktop features remain in the desktop application until they are scheduled.
- **FR-020**: Installation of the platform MUST not require a paid licence for the pilot phase (see Assumptions).

### Key Entities *(include if feature involves data)*

- **Production Order (REQUEST)**: A customer or internal order that is planned, assigned to a workstation, and moved through operations until completed.
- **Operation / Request Operation**: One manufacturing step of a production order, with its status and planned and actual time.
- **Trolley / Trolley Operation**: A physical carrier of parts that is scanned at each operation.
- **Work Record (Time Record)**: The start and stop of an operator's work on an operation, with elapsed time, cost, and good and defective quantities.
- **User / Role**: A person who signs in, with a role that defines what they can see and do.
- **Document**: A controlled document linked to production orders, items, products, projects or operations (`DOCUMENT` table).
- **Department**: The organisational unit (`DEPARTAMENTS` table). Process definitions are out of scope for the web interface in this release (OQ-003, option C).
- **Machine / Workstation**: A place where operations are performed and where scanning takes place.
- **Defect (SNAG)**: A quality issue recorded against an operation or production order.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can scan a code, start work and stop work in under 30 seconds in the web interface, measured on a shop-floor workstation.
- **SC-002**: 100% of the workflows listed in User Stories 1 to 3 can be completed in the new system, verified by an acceptance checklist signed off by the product owner.
- **SC-003**: Zero records are lost or changed incorrectly during a two-week period in which the desktop application and the web interfaces are used side by side on the same database.
- **SC-004**: A change to one product's web interface or service can be deployed and rolled back without any downtime for the other two products.
- **SC-005**: When any single backend service is stopped, the other two products continue to work for 100% of their functions that do not depend on that service.
- **SC-006**: A new site can be installed from the guide in under one working day by an IT administrator who has not seen the system before.
- **SC-007**: The pilot runs with zero licence cost for the software platform and database engine.
- **SC-008**: Cost and time figures produced by the new work-registration flow match the current desktop application within 0.01% for a sample of 100 completed operations.

---

## Assumptions

- **Technology direction is fixed by the product owner**: React for the web interfaces; Microsoft SQL Server as the database engine; Entity Framework as the ORM; GitHub as the version control system. Platform and hosting choices are open (see below).
- **Desktop applications stay in use during the transition**: the WinForms applications are not retired in this feature. They continue to run against the same `FLOWVISION2` database until each module in scope has reached parity and is signed off.
- **Architecture decision required first**: the current ProcessAdmin constitution (Principle I: no microservices or web deployment without an architectural decision record) and Principle II (raw ADO.NET, no ORM) both conflict with this feature. An architecture decision record that supersedes these principles must be approved before implementation starts.
- **Target runtime for new services**: a current long-term-support version of .NET (for example .NET 10 LTS) with Entity Framework Core. Entity Framework 6 (which runs on .NET Framework 4.7.2) is the fallback if the desktop code must be shared unchanged; this choice is part of the architecture decision record.
- **Shop-floor reliability**: production sites run on a local network. The scan flow must work without internet access. Cloud hosting is acceptable for the pilot only if the scan flow still works when the internet link is down, otherwise the services run on site.
- **Hosting options for the pilot (to be decided in the architecture record)**:
  - Services and interfaces on the site's own Windows Server with IIS (no licence cost beyond the existing server) and SQL Server Express (free, database size limit 10 GB) — preferred for shop-floor reliability.
  - Cloud pilot: Azure SQL Database free offer (one per subscription), Azure App Service free tier for services, and GitHub Pages or Azure Static Web Apps free tier for the React interfaces — suitable only for demonstrations, because free tiers have usage limits and no SLA.
- **Source control and CI**: GitHub free plan for repositories; GitHub Actions for build and deployment, staying within the free minutes included in the plan.
- **Testing**: the current constitution relies on manual validation. The new services add automated tests for the data-access layer, the cost calculation (FR-018) and the scan flow, in addition to manual acceptance. This is a change to the testing strategy and must be recorded in the architecture decision record.
- **Security**: the existing security requirements still apply: no string-concatenated SQL, passwords hashed with an approved algorithm (the hashing algorithm is an open question, OQ-005), and role checks on the server side.
- **Repository layout**: the three products keep their current source locations. In this checkout FlowDokumenty and ProcessUser are folders in the same repository, `https://github.com/szyurban75/flowvision` (resolved in OQ-003, `open-questions.md`). The new platform may use one GitHub repository per product or a single monorepo; this is decided in the plan.
- **External REST integration (trolley/RFID)** remains outbound-only and is owned by one backend service.
- **Reports**: report and export features (RDLC, PDF, Excel) are migrated after the three core areas; until then the desktop application handles them.
- **Users and roles**: the existing `USERS` table and role codes are reused. The full role list is an open question (OQ-004) and must be confirmed before role-based access (FR-010) is implemented.
- **Dependencies**: SQL Server 2016 or later is available on-premises or in the pilot cloud environment; the Microsoft Chart Controls and Office components used by the desktop application remain available on the desktop machines.
