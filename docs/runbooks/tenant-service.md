# Tenant backend local development

Read the [design](../architecture/tenant-service-design.md) and [API contract](../api-contracts/tenant-service.md). Tenant is independently deployable and owns only `SRECommerce_TenantDB`. Identity remains the authentication/membership authority. The existing Angular and legacy API are unchanged.

## Build and tests

Use the already pinned local .NET SDK from Backend:

```powershell
Set-Location D:\Projects\SRECommerce\Backend
..\artifacts\dotnet10\dotnet.exe build SRECommerce.Backend.slnx -c Release -m:1 -nr:false
..\artifacts\dotnet10\dotnet.exe test SRECommerce.Backend.slnx -c Release --no-restore -m:1 -nr:false --logger trx --results-directory ..\artifacts\backend-final
```

Tests use actual SQL Server databases with unique guarded `SRECommerce_TenantTests_*` / `SRECommerce_IdentityTests_*` names. Tenant tests call the real Identity middleware through test HTTP handlers and perform real PKCE flows; they do not read Identity tables in the production Tenant runtime. Fixtures use Identity's own test helpers for setup only. Retained test databases are not automatically deleted.

## Explicit database setup

Initial migration `20261029143617_InitialTenant` creates twelve business/catalog/audit/outbox tables plus migration history. `tenant-create-database.sql` creates only the approved database if missing. The schema script checks its database name before writing and is idempotent. SQLCMD `-I` is required for filtered indexes. Review scripts before running; startup does not migrate or seed.

```powershell
Set-Location D:\Projects\SRECommerce
sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -i docs\database-migrations\tenant-create-database.sql
sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d SRECommerce_TenantDB -i docs\database-migrations\tenant-initial.sql
Set-Location Backend\Services\Tenant\SRECommerce.Tenant.API
$env:ASPNETCORE_ENVIRONMENT='Development'
& 'D:\Projects\SRECommerce\artifacts\dotnet10\dotnet.exe' bin\Release\net10.0\SRECommerce.Tenant.API.dll --initialize
```

Initialization adds missing business types/development plans only, preserving existing catalog values and businesses. Before using Tenant, explicitly run Identity's `--initialize` with the newly built Identity API to add `tenant_api` to the existing local client's allowed scopes. This preserves redirects, requirements, credentials and existing users. Identity needs no schema migration for this integration.

Native launch from Backend:

```powershell
..\artifacts\dotnet10\dotnet.exe run --project Services\Tenant\SRECommerce.Tenant.API --launch-profile tenant-http
```

Native Tenant API: `http://localhost:5082`; Identity: `http://localhost:5081`. Health `/health/live` and `/health/ready`, development OpenAPI `/openapi/v1.json`. Readiness checks schema connectivity; Identity availability is checked on authenticated business requests. Production rejects development SQL certificate trust, non-HTTPS Identity URLs and requires HTTPS for API traffic. Configure trusted certificates/TLS before production.

Tenant is bearer-only and stores no data-protection payloads/cookies. Its framework provider is explicitly ephemeral, so no unused key files need a Tenant container volume. Identity's durable key/certificate volumes remain required and unchanged. Adding cookie/protected-payload features later requires an explicit key-persistence decision.

## Docker and restricted SQL

The owner separately approved `scripts/Initialize-TenantContainerSql.ps1 -Provision`. It creates/preserves a random dedicated login, grants Tenant-schema DML only, verifies no CREATE TABLE or Identity/legacy DB access, and saves credentials to local user-secrets/ignored `.env`. It never prints credentials, changes server authentication mode or opens firewall ports. Re-running preserves the same credentials. Do not reuse the Identity runtime login for Tenant.

```powershell
Set-Location D:\Projects\SRECommerce
# Run only with explicit runtime-login provisioning approval:
.\scripts\Initialize-TenantContainerSql.ps1 -Provision
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api config --quiet
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api build identity tenant
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api up -d --no-build
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api ps
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api logs --tail 50 identity tenant
```

The explicit overlay extends the existing Identity/Mailpit profile and adds only Tenant. Windows SQL is addressed as `host.docker.internal,1433`; Tenant uses `http://identity:8080/` on the private Compose network for the Development-only HTTP hop. Tenant publishes loopback port 5082 and limits runtime to 384 MiB / 0.75 CPU. Image build memory is separate and can exceed runtime limits; avoid simultaneous heavy builds/tests on this laptop. No gateway, SQL replacement, broker/cache or other service starts.

Stop/start without deleting volumes/data:

```powershell
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api stop tenant
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api start tenant
docker stats --no-stream
```

Do not prune/reset/unregister or use `down -v`. Preserve existing images and cryptographic volumes before updating Identity. Roll back application images only against a reviewed compatible schema; do not run destructive Down migrations.

The preserved Identity image tag `srecommerce-identity:identity-checkpoint-7eaf9a8` predates the Tenant HTTP contract. If intentionally rolling back to it, stop Tenant first and use a reviewed image override; older Identity cannot authorize the new Tenant API. No Identity schema rollback is needed for this integration. Keep Tenant data/migrations and all named volumes intact.

## Real-process smoke

`scripts/smoke-tenant.mjs` requires explicit local Identity/Tenant/Mailpit URLs and local development platform-admin credentials through environment variables. Never pass credentials as CLI arguments or print them. A trusted local operator can load the existing Identity user-secrets into those environment variables; remove them after running.

```powershell
$env:TENANT_SMOKE_IDENTITY_URL='http://localhost:5081'
$env:TENANT_SMOKE_URL='http://localhost:5082'
$env:TENANT_SMOKE_MAILPIT_URL='http://127.0.0.1:8025'
$env:TENANT_SMOKE_REPORT='D:\Projects\SRECommerce\artifacts\tenant-smoke-result.json'
# Also supply TENANT_SMOKE_ADMIN_EMAIL / TENANT_SMOKE_ADMIN_PASSWORD locally.
node scripts\smoke-tenant.mjs
```

This creates one labeled sample owner/business, confirms captured email, performs PKCE, idempotent registration, settings, submit/activation/owner provisioning, quota denial, suspend/resume and logout revocation. It revokes its issued owner/admin sessions and prints only verification metadata. It does not publish DNS records, create paid subscriptions or process money.

Verified result: 34/34 live checks passed against the final Identity/Tenant images; both APIs and Mailpit are healthy, zero restarts, APIs run as UID/GID 1654. Tenant readiness returned HTTP 200 and OpenAPI 3.1.1 exposes 22 paths. Final automated suites: Identity 24/24 and Tenant 28/28, evidence in ignored `artifacts/backend-final/`.

## Backup and recovery verification

```powershell
Set-Location D:\Projects\SRECommerce
.\scripts\Verify-TenantBackup.ps1
```

The helper takes a COPY_ONLY/CHECKSUM backup of Tenant only, runs RESTORE VERIFYONLY, restores to a unique guarded `SRECommerce_TenantTests_Restore_*` name without overwrite, runs DBCC CHECKDB, and makes the clone read-only. It refuses an existing restore target. SQL Server's configured local backup/data directories are used; no database/file is removed. The verified sample restore contains one tenant and seven audit rows. Latest manifest/log paths are in ignored `artifacts/tenant-backup-manifest.json`; preserve these and the backups separately from Git.

## Limits and failure handling

Tenant rows/children are isolated by authorized scopes/query filters; SQL rowversion and serializable mutations protect local quotas. Identity outage fails closed; ownership provisioning failure keeps onboarding pending. If provisioning succeeds but Tenant commit fails, retry after reloading the version; no active Tenant authority is granted until local commit. An unchanged active owner provisioning preserves sessions; an actual role/state change revokes affected sessions.

Suspension/expiration disables effective modules. Management/settings remain available to selected authorized Owner/Manager for recovery; no commerce APIs exist in this service. External owning services must query/enforce entitlements; a stale theme/cache must not bypass them. User quota is currently a declaration for a later Identity consumption contract; only local domain/location/module constraints are enforced here.

DNS verification uses normalized names, a hashed seven-day token and bounded TXT lookup. Tests use deterministic DNS; actual domain ownership cannot be claimed without a real TXT record. Domain routing/certificates, production billing, broker outbox dispatch, distributed tracing/load tests, dedicated database provisioning and Angular integration remain explicit later work. The outbox stores minimal pending events; no consumer delivery is claimed.
