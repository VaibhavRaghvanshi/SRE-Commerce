# ADR 0003 — Independent Identity authority and local containers

Status: implemented for the approved local Identity backend; frontend cutover and production deployment pending separate approval.

## Decision

Use ASP.NET Core Identity with OpenIddict authorization code + mandatory PKCE, stored/revocable sessions and SQL Server in a separate Identity-owned database. Keep a separate stable .NET 10 Backend solution; preserve the legacy .NET 8/SQLite baseline. Import reviewed identity snapshots with explicit provenance, no automatic tenant/platform grants and a tagged one-way legacy-password upgrade.

For local containers, retain the Windows SQL Server and connect over `host.docker.internal,1433` using a dedicated restricted runtime login. No SQL container replaces existing data. Compose opts into only Identity and Mailpit, loopback ports and modest resource limits. Persist cryptographic keys/certificates and captured mail in separate named volumes. No cache/broker is required for the current local slice.

## Consequences and evidence

OIDC changes the authentication contract; Angular and legacy authority are not silently switched. Tenant membership references await a separately approved Tenant lifecycle integration. Production needs trusted TLS, managed signing/encryption certificates, protected durable keys and a real email dispatcher/delivery setup.

Twenty-four Identity tests and thirty-three original backend tests pass. Both native and container Identity/Mailpit smoke flows pass thirteen checks. Docker Linux daemon, hello-world, image build, non-root API runtime, readiness, restricted host SQL access and both container healthchecks are verified. SQL backup/restore/DBCC CHECKDB passed; original stores remain preserved. See the [completion report](../architecture/identity-completion-report.md).

Development certificate trust/HTTP and unencrypted local key-volume files are confined to this local setup; production startup has stricter guards. No global WSL limits, Windows security settings, source databases or unrelated modules were changed by Docker troubleshooting. Latest changes stay local until a separate GitHub push approval.
