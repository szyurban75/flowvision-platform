# Test notes: spec 008

## Phase 2 — admin password reset (T047), 2026-11-13

Environment: local, `FLOWVISION2_TEST` on `localhost\FI`. Identity (https 7061) and Admin (https 7083) run from `dotnet run`, sharing the key ring in `D:\fv-dev-keys`.

### Live negative tests (manual, against the running services)

| # | Case | Expected | Result |
|---|------|----------|--------|
| 1 | Reset without `If-Match` | 428 `PRECONDITION_REQUIRED` | Passed |
| 2 | Reset of unknown user 999999 | 404 `NOT_FOUND` | Passed |
| 3 | Reset with no cookie | 401 | Passed |
| 4 | Operator signs in to admin interface | 403 `INTERFACE_NOT_ALLOWED` (Identity) | Passed (Identity side). The Admin-side 403 is covered by automated test 6 below. |

Earlier live run (same session): admin sign-in 200; reset of user 1001 returned 204 after the grant of `UPDATE` on `USERS` for the dev login. The new password signed in; the old password was refused. Repeating the same `If-Match` returned 409. The stored hash starts with `AQAAAA`.

Interim failures seen before the pass, for reference: 401 from a cookie that could not be unprotected (fixed by a restart and a fresh sign-in); 400 `WEAK_PASSWORD` from an empty password in the test command; 500 from missing `UPDATE` permission on `USERS`.

### Automated tests

`tests/Platform.Services.Tests/AdminResetEndpointTests.cs`: 18 tests across the reset and sign-in classes pass; the full services project passes 32 of 32.

- 401 no cookie
- 403 operator cookie on admin
- 428 missing `If-Match`
- 400 `WEAK_PASSWORD`
- 404 unknown user
- 409 stale `If-Match`
- 204 valid reset, stored hash verifies the new password and rejects a wrong one

These tests use EF InMemory and skip the startup schema check (environment `IntegrationTests`). They do not check the SQL Server `rowversion` change, which is covered only by the live run above.

### Cleanup (2026-11-13)

- `UPDATE` grant on `dbo.USERS` for the dev login revoked by another sysadmin; verified 0 grants.
- Identity stopped; `D:\fv-dev-keys` cleared.
- Test admin (USERS_ID 1000, role A) kept in `FLOWVISION2_TEST` for now.

## Phase 3 — golden cost export (T053), 2026-11-13

`tests/ShopFloor.CostCalc.Tests/Golden/operations-golden.csv`, exported read-only from `FLOWVISION2_TEST`.

- Selection: `TIME_OF_OPERATIONS` rows with `TOW_STATUS = 'A'` and `TOW_STOP_WORK` not null. Only **34** rows exist. The task asked for 100; 34 accepted as sufficient (2026-11-13). Status `Z` rows (606) are cancelled, with zero costs, so they were not used.
- Columns: operation id, start and stop time, good and bad quantity, user cost (`USERS.USERS_COST`), machine work cost (`MACHINE.MACHINE_COST_WORK`), and stored `TOW_COST_GOOD` and `TOW_COST_BAD`.
- Formula that matches the stored money values: `hours × (user cost + machine work cost) × good / (good + bad)` and `× bad / (good + bad)`; with no quantity, the full `hours × rate` goes to good.
- **13 rows reproduce** the stored costs from the current rates (within 0.01%): 15412, 15420, 15421, 15724, 15725, 15748, 15825, 15845–15850.
- **21 rows do not reproduce** (15817–15822, 15827–15829, 15831–15842). For these, the stored cost equals `hours × machine work cost` only, so the user cost is not in it. Their users have `USERS_COST_ADDIDTIONAL = 1.0`. The current rates cannot explain the stored figures, so a golden test using current rates would fail on them. The stored values were not changed.
- The desktop stop branch (`ProcessUser/.../Srv_RequestOperation.cs` lines 494–497) divides `workTime` by the quantity share and does not show the rate in that expression. The stored data includes the rate, so the T065 port needs the product owner's rule.

**Two calculator modes (decided 2026-11-13):** `CostMode.MachineOnly` (machine work cost) and `CostMode.UserAndMachine` (user cost plus machine work cost). The golden CSV has a `mode` column: `machine_only` (21 rows), `user_and_machine` (3 rows), `either` (10 rows, both modes give the same stored value because the user cost is 0). Every golden row matches its labelled mode.

Open: the rule that picks the mode for an operation is not known. No setting in the desktop code selects it. The product owner needs to confirm the rule before T065 is finished.

Implementation: `src/Services/ShopFloor/Application/WorkCostCalculator.cs` (pure function, T065). Tests: `tests/ShopFloor.CostCalc.Tests`: 5 unit tests for the calculator and 2 golden tests (T054). All 7 pass.

## Open items

- Real `rowversion` change after reset: not covered by automated tests.
- Documents and ShopFloor cookie validation: not tested.
- Desktop check T024: separate environment, not run.
- Two empty-database tests (ScriptMigratorTests, RowVersionConflictTests): skipped by decision.
