# Baseline stabilization completion report

Date: 2026-10-28. Approved by the owner after the first audit.

## Module Name

Legacy monolith baseline stabilization. This is not a completed microservice, tenant platform or SQL Server provider migration.

## Implementation Status

Completed local implementation and verification. GitHub delivery is recorded in migration-progress.md. The owner confirmed that no earlier Git history exists, authorized adding the project to GitHub, selected the newly created `VaibhavRaghvanshi/SRE-Commerce` repository and confirmed SQL Server as the target database technology.

## Architecture

Preserved the four-project ASP.NET Core monolith and the existing Angular application. Added a separate test project, pinned the installed stable .NET 8 SDK, removed implicit schema/seeding startup behavior, and verified the application using disposable database copies. No business service was extracted.

Changes:

- Fixed `Jwt:ExpiresMinutes` binding, validated configuration at startup, made claim mapping explicit, restricted signing algorithms and enabled lifetime validation with bounded clock skew.
- Removed the source-controlled JWT key and fixed demo passwords. Local setup generates a random key in user-secrets without printing it. Existing accounts/password hashes are unchanged. The historical password printed in the storefront header was also removed.
- User-management APIs now require an authenticated Admin whose current database role is still Admin. Employees/customers cannot list, create, promote or delete accounts. Last-admin deletion/demotion is rejected in the tested sequential workflow. The UI hides/restricts those routes for employees.
- Uploads allow only matching raster image extension/MIME/signature combinations, have a 5 MiB file limit and bounded multipart request size, use generated names and return `nosniff` on served content. SVG/HTML/executable uploads are not accepted.
- Database initialization/demo seeding default to false and are rejected outside Development. Demo seeding requires an explicit local password. Removed the previous automatic migration/retry/seeding path.
- Swagger is Development-only; CORS uses explicit configured origins; production uses HTTPS redirection; sensitive auth/user responses use no-store. Added `/health/live` and `/health/ready`.
- Corrected the dev proxy to port 5027. Browser bearer tokens are attached only to the configured API prefix, not external origins or lookalike paths.
- Repaired stale Angular tests and targeted the actual routed dashboard. Added security guard/interceptor tests.
- Reproduced and fixed the SQLite decimal aggregation failure in admin reports. Decimal sums remain decimal arithmetic in .NET for SQLite; other providers retain server aggregation. Customer counts now exclude employees.
- Upgraded EF/JWT packages to 8.0.33 and the SQLite native bundle to 3.0.5 after the NuGet advisory scan identified vulnerable transitive versions. API advisory scan then reported no known vulnerable packages. This is not a certification of the installed runtime, frontend dependencies or all future deployments.

## Files Created

- `.gitignore`, `global.json`, `README.md`.
- `SRECommerce.Api/Security/SecurityPolicies.cs`.
- `Tests/SRECommerce.Api.Tests/SRECommerce.Api.Tests.csproj`, `ApiFactory.cs`, `BaselineTests.cs`, `UploadTests.cs`.
- `SRECommerce.Web/src/app/core/guards/user-management.guard.ts` and its spec.
- `SRECommerce.Web/src/app/core/interceptors/auth.interceptor.spec.ts`.
- `scripts/Initialize-LocalSecrets.ps1`, `scripts/smoke-baseline.mjs`.
- This report and `docs/runbooks/baseline-verification.md`.
- `docs/decisions/0002-baseline-security-and-verification.md`.

## Files Modified

- `SRECommerce.sln`; API and Infrastructure csproj files.
- API `Program.cs`, `Auth/JwtOptions.cs`, `Controllers/AuthController.cs`, `Controllers/AdminUsersController.cs`, `Controllers/AdminUploadsController.cs`, `Controllers/AdminReportsController.cs`.
- API `appsettings.json`, `appsettings.Development.json`, `SRECommerce.Api.http`; Infrastructure `Data/DbSeeder.cs`.
- Angular `package.json`, `proxy.conf.json`, `app.routes.ts`, auth interceptor, header template, shop chat helper, admin layout/dashboard component and template.
- Angular existing app/account/dashboard specs; migration progress documentation.

Previously existing project source/assets were imported into Git as existing application files, not represented as newly implemented features. Binary Office/PDF deliverables, uploads and databases remain local and excluded from Git.

## Database Changes

No schema/data/provider changes to either source database. Before editing, created a consistent SQLite backup using VACUUM INTO and verified integrity, foreign keys and all eight table counts. Preserved source/media in a pre-change ZIP archive.

Created a SQL Server COPY_ONLY backup with CHECKSUM/COMPRESSION, ran RESTORE VERIFYONLY, restored to the separate `SRECommerce_BaselineRestore_20261028232640` database, ran DBCC CHECKDB successfully and made that restored clone read-only. It contains the same six business tables and historical migration table. Original SRECommerceDb was not migrated or reset. Restore clone is deliberately retained; no destructive cleanup was performed.

Local backup evidence and absolute destinations are in the ignored `artifacts/baseline-20261028-232640/` manifests. The SQL .bak resides in the engine's default backup directory. Source SQLite hash preservation is checked in the final local evidence.

## API Endpoints

Existing route contracts retained. Hardened `/api/admin/users` CRUD/role changes, `/api/admin/uploads`, `/api/auth/login`/`me`, and `/api/admin/reports/summary`. Added GET `/health/live` and GET `/health/ready`. Catalog/home/order routes were exercised without changing their schemas.

## Tests

| Command / check | Result |
| --- | --- |
| Original `dotnet build SRECommerce.sln --no-restore` under pinned SDK | Passed, zero warnings/errors |
| `dotnet build SRECommerce.sln -c Release --no-restore` | Passed, zero warnings/errors before package patching; subsequent Release test command rebuilt patched projects successfully |
| `dotnet test SRECommerce.sln -c Release --logger trx` after package patching | 33 passed, 0 failed, 0 skipped |
| Angular CLI `test --watch=false --browsers=ChromeHeadless` | 12 passed |
| Angular CLI `build --configuration production` | Passed; initial browser bundle about 386 kB, within configured budgets. Rebuilt after removing the header credential. |
| `dotnet list SRECommerce.Api/...csproj package --vulnerable --include-transitive --format json` | No known vulnerable API packages reported after patching |
| `node scripts/smoke-baseline.mjs` with explicit isolated API/web URLs | 13 HTTP checks passed against Kestrel and the built Express/Angular host; 1,000 products and 10 home sections |
| SQLite backup integrity/FK/counts | Passed |
| SQL backup checksum/restore/checkdb | Passed |

Backend tests cover real SQLite EF behavior and real authentication/authorization middleware through TestServer, including expired/wrong-key/wrong-issuer/wrong-audience tokens, privilege escalation across all account operations, stored-role revocation, last-admin safeguards, raster uploads, oversize/anonymous/customer denials, health/CORS, production seeding rejection and checkout/history/stock behavior. These are not tenant-isolation or SQL Server migration tests.

## Integration

Verified current Angular production host → proxy → Kestrel → restored SQLite database. Customer registration/login/order writes were made only to that restored smoke copy. The original source stores are preserved. SQL Server is the approved target; initial Identity extraction should own `SRECommerce_IdentityDB`, with reviewed user/hash/ID mapping. Catalog and other data remain under their current owner until their individual migration phases.

## Issues and limitations

- Still single-tenant; full Identity lifecycle, refresh/revocation, lockout/rate limiting, MFA and tenant permissions remain pending.
- Stock reservation/concurrency and order idempotency remain pending. Last-admin count checks are verified sequentially, not claimed as a complete concurrent identity invariant.
- SQLite decimal summary fallback materializes monetary projections and is appropriate only for this small legacy baseline; production analytics needs indexed/owned read models.
- Signature validation is not complete image decoding/re-encoding, malware scanning or object-storage isolation.
- Installed SDK/runtime patch maintenance and framework upgrades remain necessary; no .NET 10 SDK was installed. Angular emitted an old baseline-browser-mapping metadata warning; the build/tests still passed.
- Default configuration still points to existing SQLite by design. No connection-string-only switch to the divergent SQL Server database was attempted. Historical SQL migration source recovery remains unavailable because the owner confirms this folder had no earlier Git history.
- Docker engine remains unavailable; no container was created or started. No paid integration or real payment processing was used.
- Initial Git import retains existing whitespace/blank-EOF warnings; application source was not broadly reformatted to hide them. GitHub draft PR creation was unavailable (connector 403; browser fallback timeout); the migration branch itself was pushed and verified successfully.

## Next Recommended Task

Design and implement the Identity service on SQL Server, with the exact `SRECommerce.Identity.*` project names, `SRECommerce_IdentityDB`, reviewed legacy identity migration and independent tests. Do not start it until approved.

Checkpoint: **WAITING_FOR_USER_APPROVAL** after delivery of this baseline phase.
