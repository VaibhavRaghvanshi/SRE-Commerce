# SRE-Commerce

SRE-Commerce follows the owner's `D:\Projects\Cruds\GrowthStack` reference: one top-level folder and independent solution per microservice, plus one Angular UI folder.

```text
D:\Projects\SRECommerce\
  SRECommerce.Identity.API\
    SRECommerce.Identity.API.sln
    SRECommerce.Identity.API\Controllers\
    SRECommerce.Identity.Application\DTOs\
    SRECommerce.Identity.Application\Interfaces\
    SRECommerce.Identity.Domain\Entities\
    SRECommerce.Identity.Infrastructure\Data\
    SRECommerce.Identity.Infrastructure\Services\
    SRECommerce.Identity.Tests\
  SRECommerce.Tenant.API\                 same five-project pattern + own .sln
  SRECommerce.Theme.API\                  same five-project pattern + own .sln
  SRECommerce.Storefront.API\             same five-project pattern + own .sln
  SRECommerce.Catalog.API\                same five-project pattern + own .sln
  SRECommerce.Inventory.API\              same five-project pattern + own .sln
  SRECommerce.Pricing.API\                same five-project pattern + own .sln
  SRECommerce.ApiGateway\                 YARP API + tests + own .sln
  SRECommerce.BuildingBlocks\             shared contracts/transport helpers
  SRECommerce.Legacy.API\                 preserved original .NET 8 application/data
  SRECommerce.UI\                         existing Angular application
  docs\
  scripts\
  SRECommerce.slnx                        aggregate modern-backend solution
```

Open an individual service `.sln` to work on that API. `Controllers`, `DTOs`, `Interfaces`, `Entities`, `Data`, `Services`, `Initialization`, `Integrations` and applicable `Repositories` contain actual `.cs` source. Types were separated from combined files without changing namespaces or behavior. `bin` and `obj` are generated build outputs.

Remaining services follow this layout as implemented. **They are not complete yet**; see the [delivery matrix](docs/architecture/backend-completion-plan.md). Existing Entity Framework persistence implementations are in Infrastructure; folder organization does not invent unused repository interfaces or claim unfinished features.

## Build modern APIs

The repository pins SDK 10.0.403. The installed local SDK is under ignored `artifacts/dotnet10`; use `scripts/Install-IdentitySdk.ps1` if needed.

```powershell
Set-Location D:\Projects\SRECommerce
.\artifacts\dotnet10\dotnet.exe build SRECommerce.slnx -c Release -m:1 -nr:false
Set-Location SRECommerce.Identity.API
..\artifacts\dotnet10\dotnet.exe build SRECommerce.Identity.API.sln -c Release -m:1 -nr:false
..\artifacts\dotnet10\dotnet.exe run --project SRECommerce.Identity.API --no-launch-profile -- --urls http://localhost:5081
```

Each API owns its SQL database/migrations/tests. Startup never automatically migrates/recreates databases. Use reviewed `docs/database-migrations` scripts; native development SQL uses Windows authentication. Credentials, keys, databases, uploaded media and backups remain outside Git. Secret IDs, DB names and Docker volume names are preserved.

## Angular UI

```powershell
Set-Location D:\Projects\SRECommerce\SRECommerce.UI
npm ci
npm start
npm run build
npm run test:ci
```

Angular's workspace/output identifier is `SRECommerce.UI`. `/api` and `/uploads` retain the legacy integration on 5027. New `/commerce/{service}` routes use the gateway on 5180; `/commerce/callback` belongs to the Angular router. The production Node server accepts `API_PROXY_TARGET` and `COMMERCE_PROXY_TARGET`. New commerce authentication uses Identity authorization code + PKCE, with user tokens held in memory.

## Run the integrated Catalog workspace

Apply the reviewed per-service SQL scripts first. The launcher never migrates or recreates databases. Development uses Windows-authenticated SQL Server on the laptop.

```powershell
Set-Location D:\Projects\SRECommerce
.\scripts\Start-CommerceDevelopment.ps1 -Build -InitializeIdentity -StartUi
# Public store: http://localhost:4200/stores/legacy-demo
# Business workspace: http://localhost:4200/workspace/<tenant-id>/catalog
# Stop only processes started by this launcher:
.\scripts\Stop-CommerceDevelopment.ps1
```

The launcher starts Identity 5181, Tenant 5182, Catalog 5185, Inventory 5186, Pricing 5187 and Gateway 5180 by default. Add `-Services Identity,Tenant,Theme,Storefront,Catalog,Inventory,Pricing,ApiGateway` when all implemented APIs are needed. `-InitializeIdentity` explicitly registers the development public Angular PKCE client; it creates no confidential client secret or SQL runtime login. Private demo-owner credentials and migration mappings remain outside Git. See [local commerce runbook](docs/runbooks/commerce-local-development.md).

## Preserved legacy application

`SRECommerce.Legacy.API` contains the original four projects, regression tests, .NET 8 SDK pin and own solution. Private SQLite/media moved with the owning API directory and remain ignored; local user-secrets IDs are unchanged.

```powershell
Set-Location D:\Projects\SRECommerce\SRECommerce.Legacy.API
dotnet build SRECommerce.Legacy.API.sln -c Release
dotnet test SRECommerce.Legacy.API.sln -c Release
dotnet run --project SRECommerce.Api
```

New checkouts require restoring private data separately. Database initialization/demo seeding remain disabled. No original SQL database or Docker volume was deleted/replaced. Relocation evidence is in ignored `artifacts/source-relocation-manifest.json`.

Routine tested commits/pushes on `migration/saas-modernization` and backend/UI work together are owner-approved. See [migration progress](docs/migration-progress.md) and the [folder-layout decision](docs/architecture/source-folder-layout.md).
