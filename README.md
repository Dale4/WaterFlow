# WaterFlow API

ASP.NET Core 10 Minimal API scaffold, packaged as a Docker image.

## Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/` | Service status JSON |
| GET | `/hello` | Plain-text greeting |
| GET | `/hello2` | Plain-text greeting (v2) |
| GET | `/health` | Health check |
| GET | `/openapi/v1.json` | OpenAPI document |
| POST | `/auth/login` | Email and password. Returns a 30-minute JWT for one tenant |
| GET | `/auth/me` | Caller id, tenant, and role. Requires the JWT |
| POST | `/auth/change-password` | Signed-in user changes their password |
| POST | `/auth/forgot-password` | Starts a reset. Does not return the token |
| POST | `/auth/reset-password` | Completes a reset with an Identity token |
| POST | `/auth/users` | Operator creates a user in their tenant |
| POST | `/auth/users/{userId}/password` | Operator sets a password for a user in their tenant |

## Authentication

Accounts are ASP.NET Identity users. Passwords are hashed by Identity and are not stored or returned. After login the API trusts a short-lived JWT instead of the password.

A person belongs to a water system through a `Membership` row. The role on that row is the role in the JWT. Phase 1 roles:

| Role | Access |
| --- | --- |
| Operator | Manage users in that tenant |
| BillingClerk | Write tenant data. Cannot manage users |
| Board | Read that tenant |
| Homeowner | Read that tenant |

The first tenant is **New Avenue**. Later business tables carry `TenantId` and are filtered to the tenant in the token.

There is no public registration. Do not put the database password or JWT signing key in `appsettings.json`.

Local User Secrets on `src/WaterFlow.Api`:

```bash
dotnet user-secrets --project src/WaterFlow.Api set "ConnectionStrings:WaterFlow" "Host=localhost;Port=5432;Database=waterflow;Username=YOUR_USER;Password=YOUR_PASSWORD"
dotnet user-secrets --project src/WaterFlow.Api set "Jwt:SigningKey" "YOUR_SIGNING_KEY_AT_LEAST_32_CHARACTERS"
dotnet user-secrets --project src/WaterFlow.Api set "Jwt:Issuer" "WaterFlow"
dotnet user-secrets --project src/WaterFlow.Api set "Jwt:Audience" "WaterFlow"
```

Optional local operator, created on startup only when both values are set:

```bash
dotnet user-secrets --project src/WaterFlow.Api set "Seed:OperatorEmail" "operator@example.com"
dotnet user-secrets --project src/WaterFlow.Api set "Seed:OperatorPassword" "YOUR_PASSWORD"
```

Production reads `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, and `DB_PASSWORD` (already set on the Fargate task). Also set `JWT_SIGNING_KEY`, `JWT_ISSUER`, and `JWT_AUDIENCE` on the task before deploying an image that includes login. Do not commit those values.

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

GitHub Actions runs [`.github/workflows/build-and-push-ecr.yml`](.github/workflows/build-and-push-ecr.yml) on pull requests, merges to `main`, and manual `workflow_dispatch`.

The pipeline always **builds**, then **tests**. The image is pushed only if both succeed.

- **Pull requests:** `dotnet build` then `dotnet test --no-build`
- **Merge to `main` (and manual runs on `main`):** the same build and test, then a `linux/amd64` image is pushed to ECR

Image:

`{AWS_ACCOUNT_ID}.dkr.ecr.{AWS_REGION}.amazonaws.com/waterflow/dataservices`

Tags: git SHA and `latest`.

Set these repository variables under **Settings → Secrets and variables → Actions → Variables** before the first push to ECR:

| Variable | Purpose |
| --- | --- |
| `AWS_ROLE_ARN` | IAM role trusted by GitHub OIDC for this repo |
| `AWS_REGION` | Region of the ECR repository |
| `AWS_ACCOUNT_ID` | Account ID used to form the ECR registry URL |

The role must allow ECR push to `waterflow/dataservices` (`ecr:GetAuthorizationToken` plus `PutImage` and layer-upload actions on that repository). GitHub does not update Fargate.

## Deploy to Fargate

After the image is in ECR, and after the app-platform env exists, point the service at `:latest`:

```powershell
.\scripts\deploy-to-fargate.ps1
.\scripts\deploy-to-fargate.ps1 -Env test
```

Dev API (HTTP, path prefix `/waterflow`):

```powershell
curl http://app-plat-dev-926912358.us-west-1.elb.amazonaws.com/waterflow/hello
curl http://app-plat-dev-926912358.us-west-1.elb.amazonaws.com/waterflow/health
```

That URL works after an image that includes `UsePathBase` is pushed to ECR and this script has been run. The ALB only forwards `/waterflow/*`; without PathBase, `/waterflow/hello` is a 404 with an empty body.

Run this with your AWS CLI user (not GitHub). Use it when a new image has been pushed, and again after you destroy and recreate the env (CDK comes up on nginx until this script runs).

The script keeps the existing ALB target on port 80 (that cannot be changed on a live service) and runs the container as root so Kestrel can bind `:80`. The aspnet image otherwise exits with `Permission denied`.

Before deploying a build that includes login, add `JWT_SIGNING_KEY`, `JWT_ISSUER`, and `JWT_AUDIENCE` to the Fargate task definition. The deploy script keeps existing environment variables and only changes the image.

Requires AWS CLI credentials that can describe/update the ECS service, register a task definition, `iam:PassRole` the task roles, and attach `AmazonECSTaskExecutionRolePolicy` to the task execution role (so Fargate can pull from private ECR).
