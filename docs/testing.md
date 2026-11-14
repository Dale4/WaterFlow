# Testing WaterFlow

WaterFlow uses [xUnit](https://xunit.net/) for API tests. The test project is [`tests/WaterFlow.Api.Tests`](../tests/WaterFlow.Api.Tests).

## How to run

From the repository root:

```bash
dotnet test WaterFlow.slnx
```

In VS Code / Cursor, run the `test` task from `.vscode/tasks.json`.

## Unit vs integration

| Kind | Host | What it covers |
| --- | --- | --- |
| Unit | None | Response values from `WaterFlowEndpoints` |
| Integration | `WebApplicationFactory<Program>` | HTTP status, body, and JSON for each documented route |

`WebApplicationFactory<Program>` needs a public `Program` type. The API exposes that with `public partial class Program { }` at the bottom of [`src/WaterFlow.Api/Program.cs`](../src/WaterFlow.Api/Program.cs).

## What each test covers

**Unit** (`WaterFlowEndpointsTests`)

- `GetStatus()` returns service `WaterFlow` and status `running`
- `GetHello()` returns `Hello from WaterFlow`

**Integration** (`ApiIntegrationTests`)

- `GET /` → 200, `application/json`, `service` / `status` match
- `GET /hello` → 200, body `Hello from WaterFlow`
- With `ASPNETCORE_PATHBASE=/waterflow`, `GET /waterflow/hello` → 200 (ALB path)
- `GET /health` → 200, body `Healthy`
- `GET /openapi/v1.json` → 200, JSON with `openapi` and `/hello` in `paths`
- `GET /missing` → 404

**Authentication** (`AuthIntegrationTests`)

The host uses SQLite, not RDS. Identity and JWT settings are supplied by the test factory.

- Successful login returns a JWT that `GET /auth/me` accepts for that tenant
- A platform admin who omits `tenantId` gets a platform token; sending `tenantId` returns that membership
- `POST /tenants` creates a tenant whose operator can log in
- `POST /tenants/{tenantId}/operators` adds another Operator, including an existing username without changing that password
- `POST /platform-admins` adds another platform admin, including an existing username without changing that password
- `DELETE /platform-admins/{userId}` removes that admin and refuses to remove the last one
- The startup seed is created only when no platform admin exists. After it is deleted, a restart does not recreate it while another admin remains, and does recreate it when none remain
- Wrong password returns `invalid_credentials`
- Five failed logins lock the account
- `GET /auth/me` without a token returns 401
- A member of another tenant cannot log in as New Avenue or set a New Avenue user's password

The integration class uses `IClassFixture<WaterFlowApiFactory>` so one test host is created per class.

## Adding tests

- Put new unit tests next to the type they call (no HTTP client).
- Put new HTTP tests in `ApiIntegrationTests` (or a new `IClassFixture<WaterFlowApiFactory>` class) so they share the same factory.
