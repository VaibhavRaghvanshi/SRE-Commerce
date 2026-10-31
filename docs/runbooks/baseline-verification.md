# Baseline verification and recovery

Run from the repository root using PowerShell. These instructions preserve the source databases.

## Repeat local checks

```powershell
.\scripts\Initialize-LocalSecrets.ps1
Push-Location SRECommerce.Legacy.API
dotnet restore SRECommerce.Legacy.API.sln
dotnet test SRECommerce.Legacy.API.sln -c Release
dotnet list SRECommerce.Api\SRECommerce.Api.csproj package --vulnerable --include-transitive
Pop-Location
Set-Location SRECommerce.UI
$env:CHROME_BIN = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$env:NG_BUILD_MAX_WORKERS = '1'
npm ci
npm run test:ci
npm run build
```

`test:ci` embeds the browser/watch flags in package.json, avoiding argument-forwarding differences across Windows npm wrappers. Chrome runs headless. Baseline API tests create separate GUID-named databases/upload directories under the OS temporary directory, with generated signing keys/passwords. They check the database path before creating schema. Test results appear under the ignored TestResults directory; temporary test copies are retained for inspection, not silently deleted.

## Backup evidence

The initial backup run is recorded in the ignored `artifacts/active-baseline-path.txt`. Inside that directory:

- `source-and-uploads-before.zip`: existing source, documentation and media; excludes dependencies/build output/logs and the live SQLite database. It contains historical configuration, so keep it local and protected.
- `srecommerce.backup.db` and `sqlite-manifest.json`: consistent SQLite snapshot, source hash, table counts, integrity/FK results.
- `sql-manifest.json`, `sql-backup-and-restore.sql`, `sql-backup-and-restore.log`: SQL backup destination, exact restore-clone name and verification evidence. The SQL backup itself is in SQL Server's default Backup directory.
- `smoke/`: disposable restored SQLite database and upload root. Smoke writes are confined here.
- `smoke-result.json`: endpoint/status summary; no tokens, passwords or customer contact values.

Keep both source stores and the backup/restore clone. No volume/database deletion is part of this runbook. Repeating SQL restore must use new unique database/file names and refuse existing targets; never add WITH REPLACE against either source.

## Reproduce the real-process smoke test

Create a **new** smoke directory, copy the verified backup into `smoke.db`, and create its own `wwwroot` directory. The clone is disposable; the source database is not. Use full paths in the following environment overrides. The dev JWT key comes from the local user-secrets setup.

API terminal, from `SRECommerce.Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:15270'
$env:ASPNETCORE_WEBROOT = '<absolute-smoke-directory>\wwwroot'
$env:ConnectionStrings__DefaultConnection = 'Data Source=<absolute-smoke-directory>\smoke.db;Mode=ReadWrite'
$env:Database__Initialize = 'false'
$env:DemoData__Enabled = 'false'
dotnet bin\Release\net8.0\SRECommerce.Api.dll
```

Web terminal, from `SRECommerce.UI`, after the production build:

```powershell
$env:PORT = '14201'
$env:API_PROXY_TARGET = 'http://127.0.0.1:15270'
node dist\SRECommerce.UI\server\server.mjs
```

Check terminal, from the repository root:

```powershell
$env:SRE_SMOKE_API_URL = 'http://127.0.0.1:15270'
$env:SRE_SMOKE_WEB_URL = 'http://127.0.0.1:14201'
$env:SRE_SMOKE_REPORT = '<absolute-smoke-directory>\smoke-result.json'
node scripts\smoke-baseline.mjs
```

The smoke script intentionally creates a generated test customer and one order and decrements stock in the clone. Do not point it at an existing application/shared/production database. Stop the two processes with Ctrl+C afterwards; close those terminals to discard their process-scoped overrides. The normal dev proxy uses port 5027; these separate smoke ports avoid affecting normal development.

## Restore before migrating

The verified SQL restore clone is read-only and contains only the older schema. It is not a replacement for the richer SQLite snapshot. Do not run the new monolith against it by changing a connection string: its provider/schema do not match. The approved target is SQL Server, with explicit per-service mapping/migrations and data reconciliation at each approved extraction.

Rollback of source changes uses the pre-change archive or Git review history as appropriate; no automatic database rollback is required because this phase did not modify source data. Keep existing account passwords unchanged; use a reviewed migration/reset process rather than reseeding them.

## Security references

The implementation follows [Microsoft JWT validation guidance](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication) and [file upload validation guidance](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads). This baseline retains the existing custom login JWT contract for compatibility; the approved future Identity service must review standards-based OAuth2/OIDC ownership separately.
