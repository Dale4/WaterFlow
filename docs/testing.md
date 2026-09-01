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
- `GET /health` → 200, body `Healthy`
- `GET /openapi/v1.json` → 200, JSON with `openapi` and `/hello` in `paths`
- `GET /missing` → 404

The integration class uses `IClassFixture<WaterFlowApiFactory>` so one test host is created per class.

## Adding tests

- Put new unit tests next to the type they call (no HTTP client).
- Put new HTTP tests in `ApiIntegrationTests` (or a new `IClassFixture<WaterFlowApiFactory>` class) so they share the same factory.
