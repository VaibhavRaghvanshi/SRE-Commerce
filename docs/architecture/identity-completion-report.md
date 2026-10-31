# Identity backend completion and Docker verification

Status: approved Identity backend implemented and locally verified. Docker follow-up completed. **WAITING_FOR_USER_APPROVAL** for any next module, frontend/authority cutover or GitHub push. This is a local development result, not a production rollout.

## Implementation and ownership

Exactly five projects under `SRECommerce.Identity.API`: API, Application, Domain, Infrastructure and Tests. Separate Backend solution/SDK pin preserves the original .NET 8 application. .NET SDK 10.0.403, Microsoft Identity/EF/runtime 10.0.14, OpenIddict 7.7.2; explicit cryptography packages retain patched versions. API dependency advisory scan reported zero known vulnerabilities at verification.

OpenIddict authorization code + S256 PKCE, exact registered redirect, server-side antiforgery login, verified-email policy, lockout, MFA/recovery codes, five-minute access tokens, rotating stored refresh tokens and session revocation are implemented. API authorization checks current account/session/security stamp and stored token/authorization validity. Tenant scope comes from the validated token; memberships and permissions are checked against SQL. Platform SuperAdmin is explicit and audited. Concurrent last-owner demotions are serialized. See [API contract](../api-contracts/identity-service.md) and [design/ERD](identity-service-design.md).

Source/configuration includes the five projects, Backend solution/SDK/tool pins, reviewed SQL scripts, Dockerfile/Compose, local setup/import/smoke scripts and runbooks. Root changes are limited to ignore rules, Docker packaging and documentation. No Angular authority cutover, gateway, Tenant or Notification service was created.

## Database and legacy preservation

Approved database: `SRECommerce_IdentityDB`. Nineteen tables including migration history:

| Area | Tables |
| --- | --- |
| Account framework | Users, Roles, UserRoles, AspNetRoleClaims, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens |
| Profile/access | UserProfiles, TenantMemberships, RolePermissions |
| Security/provenance | UserSessions, SecurityAuditLogs, LegacyUserMappings, EmailOutbox |
| Protocol | OpenIddictApplications, OpenIddictAuthorizations, OpenIddictScopes, OpenIddictTokens |
| Migration history | __EFMigrationsHistory |

Three reviewed migrations were applied during the earlier approved Identity implementation: InitialIdentity, BoundIdentityTokenKeys, BoundOidcSubjects. Final user-table index keys fit SQL Server's limits; narrowing migrations check existing lengths first. The full [idempotent script](../database-migrations/identity-initial.sql) requires SQLCMD `-I`. Docker troubleshooting introduced no migrations/schema changes.

Legacy snapshots contained three SQLite users and three SQL users, with one shared normalized email. Five distinct accounts were imported and six provenance mappings retained; the reviewed alias preserves the current SQLite credential. Repeating import creates no additional accounts/mappings. No imported account was automatically email-confirmed, tenant-assigned or granted SuperAdmin. Tagged BCrypt compatibility rehashes successful legacy credentials into native Identity; wrong/oversized legacy inputs cannot clear the marker, and native hashes cannot fall back to old credentials.

Original SQLite hash remains identical to the baseline backup (1,000 products/three orders). Original SQL store remains three users/ten products. Only Identity development/test databases received Identity writes. The target had eight accounts after the native and container smoke samples, including five legacy accounts, one explicit development bootstrap admin and two labeled smoke accounts. Snapshot/credential exports are ignored and local only.

## Verification evidence

| Check | Result |
| --- | --- |
| Identity Release build / real SQL-backed tests | Passed; 24 tests, zero failures |
| Original backend regression tests | 33/33 passed again |
| Native API + local Mailpit smoke | 13/13 passed |
| Docker Client/Server, Linux mode, docker ps | Passed; 28.3.3 / linux |
| hello-world pull/run | Passed |
| Multi-stage Identity Docker build | Passed; 19 build steps completed |
| Identity/Mailpit startup | Both healthy, zero restarts |
| Container runtime user | Non-root UID/GID 1654 |
| Host SQL from container | Passed readiness and real account/token operations through restricted login |
| Container HTTP / protocol / SMTP smoke | 13/13 passed; health 200, OpenAPI 3.1.1 / 22 paths |
| SQL runtime privileges | No CREATE TABLE or legacy-database access |
| SQL backup/restore | COPY_ONLY/CHECKSUM, VERIFYONLY, isolated restore, CHECKDB, read-only restore passed |

Tests cover SQL migrations, rowversion, registration/confirmation/reset, password and account revocation, MFA/recovery, lockout/rate limiting, refresh replay, invalid protocol requests, cross-tenant access, owner concurrency, legacy import/hash compatibility, and encrypted email retention/retry during SMTP outage. Real-process tests generate temporary development credentials and never print token/password values.

Ignored evidence: `artifacts/identity-smoke-result.json`, `artifacts/identity-container-smoke-result.json`, `artifacts/identity-backup-manifest.json`, `artifacts/identity-backup-restore.log`. Restore database `SRECommerce_IdentityTests_Restore_20261029081528` and integration-test databases remain available; none were dropped. Backup location is recorded in the local manifest. SQL restore verification occurred before the later container smoke sample; it is recovery evidence for that earlier snapshot.

## Deployment limits and next checkpoint

Local email uses Mailpit and a single at-least-once SQL outbox dispatcher. Production SMTP, multi-replica leasing/deduplication, real frontend consent/login UX, Tenant lifecycle checks, federation, telemetry/load testing and production backup/key recovery operations remain separate deployment/integration work. Existing Angular and legacy API still use their original authentication. Container development certificates/key ring are separate from native development keys; production requires managed certificates, protected key storage and HTTPS/trusted SQL TLS. Never expose the local Development profile as production.

Only Identity and Mailpit run; no SQL replacement, cache, broker or Kafka was started. Docker work made no Windows feature, firewall, BIOS or SQL authentication-mode changes. Native Mailpit was stopped to release its ports; its data remains intact. No destructive cleanup, GitHub push, merge or deployment occurred. Review [Docker guide](../docker-local-development.md) and [Identity runbook](../runbooks/identity-service.md), then choose the next approved task.
