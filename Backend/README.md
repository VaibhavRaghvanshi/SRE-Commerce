# SRECommerce backend source

Open **`D:\Projects\SRECommerce\Backend\SRECommerce.Backend.slnx`** in Visual Studio (with .NET 10 support), or open this Backend folder in your editor. The older solution at the repository root is the preserved legacy application.

`bin\Release\net10.0` contains compiled DLLs after a build. `obj` contains temporary build files. Neither folder contains the editable C# controllers.

Each service has five separate projects:

```text
Services/<Service>/
  SRECommerce.<Service>.API/
    Controllers/                 <-- HTTP endpoint source (.cs files)
    <Service>Program.cs           <-- startup and dependency registration
    appsettings.json
    SRECommerce.<Service>.API.csproj
  SRECommerce.<Service>.Application/    contracts and use-case boundaries
  SRECommerce.<Service>.Domain/         entities and domain validation
  SRECommerce.<Service>.Infrastructure/ owned SQL persistence and adapters
  SRECommerce.<Service>.Tests/          automated verification
```

Identity uses `Program.cs` for startup. The other existing service entry-point classes/files include the service name. Controller namespaces do not depend on filesystem folders.

## Actual implementation status

| Service | Source location | Verification/status |
| --- | --- | --- |
| Identity | [Controllers](Services/Identity/SRECommerce.Identity.API/Controllers) | Previously verified backend; new commerce/machine-access extension under verification |
| Tenant | [Controllers](Services/Tenant/SRECommerce.Tenant.API/Controllers) | Previously verified backend; new public registry/member-entitlement extension under verification |
| Theme | [Controller](Services/Theme/SRECommerce.Theme.API/Controllers/ThemeController.cs) | 17 full-suite tests plus additional SQL-outage test passed; owned SQL migration/backup/restore verified; new container runtime pending |
| Storefront | [Controller](Services/Storefront/SRECommerce.Storefront.API/Controllers/StorefrontController.cs) | 25/25 domain and real SQL integration tests passed; main database migration/backup and Docker runtime acceptance still pending |
| Catalog through Discovery | See [delivery matrix](../docs/architecture/backend-completion-plan.md) | Remaining implementation pending; folders are not generated as a completion claim |
| Gateway | See delivery matrix | Pending |

All backend services are **not complete yet**. Latest owner instruction (2026-10-31) authorizes developing Angular alongside backend and pushing tested checkpoints to the existing migration branch without asking each time. Earlier sequential Angular/push gates are superseded. Native Windows-auth SQL development/tests continue; new runtime credential scripts have not been executed.
