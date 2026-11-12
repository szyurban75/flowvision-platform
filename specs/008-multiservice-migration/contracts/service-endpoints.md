# Service Endpoints (draft)

**Feature**: 008-multiservice-migration | **Status**: draft. Finalise in ADR-001 and in the first slice.

All endpoints are under `/api/<service>/`. Each service is reachable only through IIS on the site LAN. No endpoint is exposed to the internet. Authentication is per web interface (clarification Q1-B): every request carries a session issued by the Identity service for that interface.

## Common

- **Errors**: JSON body `{ "message": "<plain-language text>", "code": "<stable code>" }`. Technical details go to the log, never to the response (FR-014).
- **Concurrency**: reads return an `ETag` derived from the row version. Writes send `If-Match`. A mismatch returns `409 Conflict` with code `CONCURRENCY` and the message "This record was changed by someone else. Reload and try again." (FR-012).
- **Unavailable service**: `503` with code `SERVICE_UNAVAILABLE`.
- **Health**: `GET /api/<service>/health` returns service status and the schema version it expects and found (FR-006).

## Identity service

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/identity/sign-in` | Sign in with `USERS` credentials for one web interface. Body: `{ "login", "password", "interface" }`. |
| POST | `/api/identity/sign-out` | End the session for that interface. |
| GET | `/api/identity/me` | Current user and roles for that interface. |

## ShopFloor service (User Story 1)

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/shopfloor/codes/{code}` | Resolve a trolley or QR code to the order and operation. `404` with code `UNKNOWN_CODE` if no match; no record changed. |
| POST | `/api/shopfloor/operations/{id}/start` | Start work on an operation. Creates a work record with operator, operation and start time. |
| POST | `/api/shopfloor/work-records/{id}/stop` | Stop work. Body includes good and defective quantities. Returns elapsed time and cost (FR-018). Sends `If-Match`. |

## Admin service (User Story 2)

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/admin/production-orders?state=...` | List orders. |
| GET | `/api/admin/production-orders/{id}` | One order, with `ETag`. |
| POST | `/api/admin/production-orders` | Create an order with required fields. |
| PUT | `/api/admin/production-orders/{id}` | Edit an order. Sends `If-Match`. |
| POST | `/api/admin/production-orders/{id}/assign` | Assign to a workstation and an operator. |
| GET | `/api/admin/supervision` | Active and waiting orders for the supervision screen. Clients poll every 15 s (freshness rule, 30 s max). |
| GET | `/api/admin/kpi` | KPI dashboard figures. |

## Documents service (User Story 3)

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/documents` | List documents, filterable by production order, item, product, project and operation. |
| POST | `/api/documents` | Register a document and link it to production order, item, product, project or operation. |
| GET | `/api/documents/{id}` | One document, with `ETag`. |
| PUT | `/api/documents/{id}` | Update a document. Sends `If-Match`. |
| GET | `/api/documents/departments` | Department definitions. |

## Open items

- Exact request and response schemas are written in the first slice (ShopFloor), then copied to the other services.
- Role checks (FR-010) apply to every endpoint; the role list depends on OQ-004.
- Export endpoints (PDF, Excel) are out of scope for the first release (FR-019).
