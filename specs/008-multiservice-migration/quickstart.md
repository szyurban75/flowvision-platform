# Quickstart: FlowVision Platform (planned)

**Feature**: 008-multiservice-migration | **Status**: planned, not yet verified. These steps describe the intended setup. They are checked when the first slice is built.

## Prerequisites

- Windows 10 or 11, or Windows Server 2022.
- .NET 10 SDK (LTS).
- Node.js 22 LTS (for the React builds).
- SQL Server Express 2022 or Developer edition, with two databases: `FLOWVISION2_TEST` (for development) and `FLOWVISION2` (only for the pilot, and only with the desktop application closed while the migration runs).
- Git and access to the GitHub repository.

## 1. Get the code

```text
git clone <repository URL>
git checkout 008-multiservice-migration   (or main once merged)
```

## 2. Create the test database

Restore a copy of `FLOWVISION2` to `FLOWVISION2_TEST`. Never point development at production `FLOWVISION2`.

## 3. Apply migrations

Run the migration runner against `FLOWVISION2_TEST`. It applies `db_*FlowVision*.sql` scripts in version order and records the schema version.

```text
dotnet run --project platform/src/Tools/MigrationRunner -- --connection "<FLOWVISION2_TEST connection>" --scripts "ProcessAdmin/ProcessAdmin/ProcessAdmin/database"
```

## 4. Start the backend services

Each service reads the schema version and refuses to start on a mismatch (FR-006).

```text
dotnet run --project platform/src/Services/Identity
dotnet run --project platform/src/Services/Admin
dotnet run --project platform/src/Services/ShopFloor
dotnet run --project platform/src/Services/Documents
```

## 5. Start the web interfaces

```text
cd platform/web/shopfloor
npm install
npm run dev
```

Repeat for `admin` and `documents`.

## 6. Run the tests

```text
dotnet test platform/Platform.sln
npm test --prefix platform/web/shopfloor
npx playwright test --project=smoke
```

## 7. Manual acceptance

Follow the acceptance scenarios in spec.md for the user story in scope. For the first slice, scan a known trolley code, start and stop work, and compare the saved record with the desktop application (constitution Principle VI).

## Pilot installation (site)

The full site guide (FR-017) is written as part of the first slice. It will cover IIS bindings, Windows service install, the self-hosted GitHub runner, and the schema check. Target: installable by an IT administrator in under one working day (SC-006).
