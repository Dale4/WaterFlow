# WaterFlow API

ASP.NET Core 10 Minimal API scaffold, packaged as a Docker image.

## Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/` | Service status JSON |
| GET | `/hello` | Plain-text greeting |
| GET | `/health` | Health check |
| GET | `/openapi/v1.json` | OpenAPI document |

## Run locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/WaterFlow.Api
```

The HTTP profile listens on `http://localhost:5080`.

```bash
curl http://localhost:5080/hello
curl http://localhost:5080/health
```

## Tests

```bash
dotnet test WaterFlow.slnx
```

See [docs/testing.md](docs/testing.md) for the unit vs integration split and what each test covers.

## Docker

Build and run the image:

```bash
docker build -t waterflow-api .
docker run --rm -p 8080:8080 waterflow-api
```

Then:

```bash
curl http://localhost:8080/hello
curl http://localhost:8080/health
```
