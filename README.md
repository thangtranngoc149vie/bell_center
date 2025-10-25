# Bell Center Notifications API

This repository hosts a .NET 8 Web API that exposes the Bell Center notification endpoints described in `FISA_Bell_Center_APIs_v1.0`. The implementation uses ASP.NET Core, PostgreSQL, and Dapper with schema and behaviour that align with the source specification.

## Project structure

```
.
├── BellCenter.sln
├── db
│   └── migrations
│       └── migration_notifications_bell_center_v1_0.sql
├── openapi
│   └── bell-center-api.yaml
└── src
    └── BellCenter.Api
        ├── BellCenter.Api.csproj
        ├── Controllers
        │   └── NotificationsController.cs
        ├── Infrastructure
        │   ├── INotificationRepository.cs
        │   └── NotificationRepository.cs
        ├── Models
        │   ├── NotificationListQuery.cs
        │   ├── NotificationListRequest.cs
        │   └── NotificationModels.cs
        ├── Options
        │   └── SignalRNegotiationOptions.cs
        ├── Program.cs
        ├── Properties
        │   └── launchSettings.json
        ├── Support
        │   ├── IUserContext.cs
        │   └── UserContext.cs
        ├── appsettings.Development.json
        └── appsettings.json
```

## Prerequisites

* .NET SDK 8.0
* PostgreSQL 14+ (or a compatible managed service)
* Access to an authentication provider capable of issuing JWTs that include a `sub` or `nameidentifier` claim matching the application user id.

> **Note**: The execution environment used to generate this change set does not contain the .NET SDK, therefore build and test commands were not run automatically. Ensure the SDK is installed locally before running the steps below.

## Getting started

1. **Database setup**
   * Ensure the extension `uuid-ossp` (or equivalent UUID generator) is available so that `uuid_generate_v4()` works.
   * Apply the migration script:
     ```bash
     psql $CONNECTION_STRING -f db/migrations/migration_notifications_bell_center_v1_0.sql
     ```
   * Seed the `users` table if necessary; the notification tables expect valid foreign keys.

2. **Configuration**
   * Update `ConnectionStrings:Database` in `appsettings.json` (and the Development variant) with credentials for your PostgreSQL instance.
   * Configure the negotiation payload returned by `/api/v1/notifications/negotiate` via the `SignalR:Negotiation` section. The default token is empty; populate it if your SignalR host requires pre-negotiated access tokens.
   * When hosting behind a gateway, enforce RBAC scopes such as `notifications:read` and `notifications:write` to comply with the specification.

3. **Run the API**
   ```bash
   dotnet restore
   dotnet run --project src/BellCenter.Api
   ```
   The project exposes Swagger UI at `https://localhost:7143/swagger` (or the HTTP port variant) by default.

## Request handling & business rules

* **User scoping**: `UserContext` resolves the current user id from JWT claims (`sub` or `nameidentifier`). For local development, a `X-User-Id` header containing a GUID is also accepted.
* **Cursor pagination**: List endpoints use the `user_notifications.id` as a stable cursor. Supplying the `cursor` query parameter skips records up to and including that identifier. Sorting supports both `created_at_desc` (default) and `created_at_asc`.
* **Filtering**: Filters for severity, category, time window, and source metadata are translated directly into SQL predicates with proper parameter binding. Severity values are validated against the allowed set `{info, warning, critical}`.
* **Read state management**: `PATCH /{id}/read`, `POST /bulk-read`, and `PATCH /{id}/hide` operate against the `user_notifications` table. Updates are idempotent and skip rows that are already in the requested state.
* **Statistics**: Both the list response and `/stats` endpoint return unread aggregates grouped by category and severity to support badge counts and dashboards.
* **Negotiation**: `/negotiate` reflects static configuration for SignalR. Hook this endpoint into the actual negotiation workflow used by your real-time infrastructure.

## API surface

The controller implements the endpoints mandated by the specification:

* `GET /api/v1/notifications` — Cursor-based listing with filters and inline stats
* `GET /api/v1/notifications/{id}` — Detail view including payload
* `PATCH /api/v1/notifications/{id}/read` — Toggle read state
* `POST /api/v1/notifications/bulk-read` — Bulk mark-as-read either by ids or by filter set
* `PATCH /api/v1/notifications/{id}/hide` — Hide an item from the user feed
* `GET /api/v1/notifications/stats` — Aggregated unread counters
* `GET /api/v1/notifications/negotiate` — Returns SignalR negotiation metadata

An OpenAPI 3.0 document (`openapi/bell-center-api.yaml`) mirrors these routes for contract validation and client generation.

## Data access

* Dapper executes parameterised SQL queries closely modelled on the document specification.
* Repository queries:
  * Join `user_notifications` and `notifications` to avoid N+1 fetches.
  * Use server-side pagination and ordering to leverage indexes (see migration script).
  * Parse JSON payloads safely using `System.Text.Json.Nodes` while ignoring invalid JSON fragments to keep the API resilient to malformed data.

## Realtime & outbox notes

* The migration includes a minimal `outbox_events` table as a safeguard. If your platform already provides the table, the `IF NOT EXISTS` guards will keep the migration idempotent.
* A dedicated worker can poll unprocessed outbox rows and push SignalR updates. The repository keeps unread counts in sync so push notifications can refresh badges instantly.

## Testing

The recommended test plan from the specification still applies:

* Unit tests covering filter parsing and SQL parameter binding.
* Integration tests using a disposable PostgreSQL instance (e.g., Testcontainers) to validate cursor pagination, read state transitions, and statistics.
* Contract tests to ensure the API implementation matches `openapi/bell-center-api.yaml`.

> Because the authoring environment lacks the .NET SDK, `dotnet test` was not executed. Run the full test suite locally once dependencies are available.

## Deployment considerations

* Enable HTTPS and JWT validation that integrates with your identity provider.
* Configure connection pooling through `NpgsqlDataSource` (already registered) to sustain Bell Center traffic.
* When deploying to container platforms, mount `appsettings.json` overrides or use environment variables for secrets and SignalR negotiation tokens.

