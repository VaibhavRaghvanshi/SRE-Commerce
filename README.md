# SRE-Commerce

Existing ASP.NET Core and Angular commerce application, being modernized incrementally toward a configurable, multi-tenant platform with service-owned SQL Server databases.

The legacy application remains a **single-tenant monolith** on SQLite. The independent Identity backend is implemented and locally verified on SQL Server under `Backend/Services/Identity`; authority/frontend cutover is separate. Existing data is excluded from Git and must be restored separately. See [migration progress](docs/migration-progress.md) before continuing work.

Identity setup, protocol contracts and verification: [Identity runbook](docs/runbooks/identity-service.md). It has its own stable .NET 10 SDK pin/solution; the legacy .NET 8 solution remains independent.

Docker Desktop Linux engine and the Identity/Mailpit containers are verified: [local Docker guide](docs/docker-local-development.md), [Identity completion report](docs/architecture/identity-completion-report.md). Latest Identity/Docker changes remain local; a GitHub push requires separate approval.

## Prerequisites

- Stable .NET SDK 8.0.404 (pinned in global.json, latest compatible patch roll-forward; preview SDKs excluded).
- Node.js 22.15 or a compatible supported version for Angular 20.3, npm, and Chrome for headless tests.
- Existing SQL Server 2022 Developer instance for the planned migration; Docker is not required for the baseline.

.NET 8 and Angular 20 approach the end of their support windows in November 2026. The next service should use a separately approved stable .NET 10 setup; framework upgrades are not silently bundled into the database migration. See the [assessment](docs/architecture/existing-system-assessment.md) and official support links there.

## Local setup

```powershell
.\scripts\Initialize-LocalSecrets.ps1
dotnet restore SRECommerce.sln
dotnet build SRECommerce.sln -c Release
```

The script creates a random JWT key in ASP.NET user-secrets only if no local key exists; it does not print the key, initialize databases, seed accounts or change existing account passwords. User-secrets are a local development convenience, not an encrypted production secret store. Production must provide Jwt__Key using an appropriate secret provider. Never commit it.

Restore a verified SQLite backup to `SRECommerce.Api/App_Data/srecommerce.db` before running the legacy application. The default connection opens an existing database in read/write mode and does not create a new database automatically.

```powershell
dotnet run --project SRECommerce.Api --launch-profile http
```

API: `http://localhost:5027`; Swagger is Development-only. `/health/live` checks the process and `/health/ready` checks required database tables. Missing schema returns 503; startup does not apply migrations or seed data. Do not replace the SQLite connection with a SQL Server string before the provider migration is implemented.

In a second terminal:

```powershell
Set-Location SRECommerce.Web
npm ci
npm start
```

The development proxy sends `/api` and `/uploads` to port 5027. Development CORS allows explicit localhost origins on 4200/4201. Production origins must be configured explicitly; wildcard CORS is not enabled.

## Verification

```powershell
dotnet test SRECommerce.sln -c Release
Set-Location SRECommerce.Web
$env:CHROME_BIN = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm run test:ci
npm run build
```

The API suite uses generated credentials, separate temporary SQLite databases and upload folders. It never initializes the existing application database. Frontend tests cover the routed shell/account/dashboard, admin-only user management and bearer-token origin scope.

Upload validation accepts matching PNG/JPEG/GIF/WebP extensions, MIME types and signatures, limits images to 5 MiB, generates filenames, and serves `nosniff`. This is not a complete image decoder, malware scanner, media-processing service or production storage policy.

## Controlled demo data

Database initialization and demo seeding default to **false**. They are rejected outside Development. They must only be enabled intentionally against a new disposable development database. `DemoData:Password` must be supplied through a local secret provider with at least 12 characters; there is no bundled fixed password. Existing accounts and their password hashes are preserved. The seeder still generates sample catalog/orders and must not be used to reconcile or migrate real data.

## Scope and known limits

User management now requires a current stored Admin role. Employees keep catalog/order access but cannot list/create/promote/delete users. The last administrator cannot be demoted or deleted by the sequential API workflow. Full identity lifecycle, refresh/revocation, tenant isolation, stock concurrency/reservations, distributed checkout and production hardening remain future work. Do not expose this learning baseline as a production SaaS.

Backups, databases, uploads, logs, build outputs, historical Office/PDF deliverables and local secrets are excluded from Git. Original source/media and both database stores were preserved locally before stabilization. Read the [baseline report](docs/architecture/baseline-stabilization-report.md) and [runbook](docs/runbooks/baseline-verification.md) for evidence, recovery and the next approval checkpoint.
