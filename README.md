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

## CI / ECR

GitHub Actions runs [`.github/workflows/ci.yml`](.github/workflows/ci.yml) on pull requests, merges to `main`, and manual `workflow_dispatch`.

- **Pull requests:** `dotnet test WaterFlow.slnx --configuration Release`
- **Merge to `main` (and manual runs on `main`):** the same tests, then a `linux/amd64` image is built and pushed to ECR

Image:

`{AWS_ACCOUNT_ID}.dkr.ecr.{AWS_REGION}.amazonaws.com/waterflow/dataservices`

Tags: git SHA and `latest`.

Set these repository variables under **Settings → Secrets and variables → Actions → Variables** before the first push to ECR:

| Variable | Purpose |
| --- | --- |
| `AWS_ROLE_ARN` | IAM role trusted by GitHub OIDC for this repo |
| `AWS_REGION` | Region of the ECR repository |
| `AWS_ACCOUNT_ID` | Account ID used to form the ECR registry URL |

The role must allow ECR push to `waterflow/dataservices` (`ecr:GetAuthorizationToken` plus `PutImage` and layer-upload actions on that repository).
