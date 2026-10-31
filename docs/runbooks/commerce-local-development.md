# Integrated local commerce development

Use the repository root `D:\Projects\SRECommerce`. Each API has its own solution, source projects, SQL database and migrations. Native development uses Windows authentication; no SQL container replaces the installed SQL Server.

## Start and stop

```powershell
.\scripts\Start-CommerceDevelopment.ps1 -Build -InitializeIdentity -StartUi
.\scripts\Stop-CommerceDevelopment.ps1
# To rebuild one running service, stop its owned process first:
.\scripts\Stop-CommerceDevelopment.ps1 -Services Catalog
```

Ports: gateway 5180, Identity 5181, Tenant 5182, Theme 5183, Storefront 5184, Catalog 5185, Angular development 4200. Existing Docker Identity/Tenant ports 5081/5082 are separate. The launcher preserves unknown port occupants and does not run SQL migrations, reset data or provision runtime accounts. Its process manifest/logs are ignored under `artifacts/commerce-development`.

`-InitializeIdentity` registers the development public Angular client with code + PKCE, refresh tokens and explicit localhost 4200/4000 callbacks. Tokens are held in browser memory. Short-lived PKCE state uses sessionStorage. A page reload requires a new authorization redirect; existing Identity cookies can still provide SSO. Sign-out calls the Identity session-revocation endpoint. Live account/tenant/module checks remain authoritative on every protected API request.

## Browser URLs

- `http://localhost:4200/stores/legacy-demo` — published Catalog products only.
- `http://localhost:4200/workspace/<tenant-id>/catalog` — Owner/Manager product and category editor. Platform support requires a reason, audited by the API.
- `http://localhost:5180/health/live` — gateway liveness.
- `http://localhost:5185/health/ready` — Catalog SQL readiness.
- `http://localhost:5185/openapi/v1.json` — Development API contract.

The public page deliberately shows unavailable price/stock until the owning services are implemented. A Catalog publication alone never authorizes a sale. Imported Legacy Demo products are drafts; publishing is explicit. The original source SQLite and older SQL database remain preserved.

## Build and verify

```powershell
.\artifacts\dotnet10\dotnet.exe build SRECommerce.slnx -c Release -m:1 -nr:false
.\artifacts\dotnet10\dotnet.exe test SRECommerce.Catalog.API/SRECommerce.Catalog.Tests -c Release --no-build
.\artifacts\dotnet10\dotnet.exe test SRECommerce.ApiGateway/SRECommerce.ApiGateway.Tests -c Release --no-build
npm --prefix SRECommerce.UI run test:ci
npm --prefix SRECommerce.UI run build
.\scripts\Test-RepositorySecrets.ps1
```

Serialize heavy builds and real-SQL suites on this laptop. The Catalog HTTP suite creates uniquely named isolated test databases and retains them for inspection; it never resets a service/source database.

For production-server verification:

```powershell
Set-Location SRECommerce.UI
$env:COMMERCE_PROXY_TARGET='http://127.0.0.1:5180'
$env:PORT='4000'
node dist/SRECommerce.UI/server/server.mjs
# In another terminal, from SRECommerce.UI:
node scripts/smoke-commerce.mjs
```

The browser smoke uses installed Chrome and the private, dedicated Legacy Demo owner from local user-secrets. It creates and publishes one labeled smoke product and checks public listing/detail at desktop/mobile sizes. It does not print credentials or tokens. Screenshots/results are ignored in `artifacts/commerce-browser-smoke`. Angular's Node proxy streams requests/responses and bounds requests to 15 seconds; it preserves callback routes for Angular and does not retry writes.

## Current limits

Catalog native SQL/API and initial Angular integration are implemented. Binary media upload, full brand/collection admin UX and cross-service pricing/stock/cart integration remain pending. Gateway exposes only implemented services, strips spoofed actor/forwarding headers and cookies, and blocks internal service paths; downstream APIs enforce authorization. Production edge deployment, telemetry and dependency-readiness checks remain pending. Runtime SQL logins, confidential service clients and broker/cache accounts are still unprovisioned under the earlier hold. RabbitMQ delivery is not claimed merely because outbox rows exist. Kafka remains deferred.
