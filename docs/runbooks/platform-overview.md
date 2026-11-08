# Platform analytics, plans and service visibility

`/platform/workspace` includes actual Tenant aggregates, subscription-plan creation, platform audit history and on-demand service health. Existing Tenant/Identity authorization resolves current administrator status; an owner/customer bearer cannot use the aggregate or probe endpoints. Reading either new endpoint adds a platform audit entry. No service databases are joined, queried with a shared login or exposed through support impersonation.

## Counts ka meaning

`GET /api/v1/platform/overview` reports the sampled time, total tenants grouped by lifecycle/business type, active plan count, effective subscriptions and current terms ending within seven days. Effective means an Active tenant with a Trial/Active subscription whose start/end contain the sampled time. Cancelled, future, expired and suspended-business subscriptions are excluded.

The last thirty UTC registration days include zero-count dates. Stored timestamp offsets are converted to UTC before grouping. Module counts mean configured enabled switches on active tenants; they are not proof that a current plan grants access. Every normal service request still evaluates its actual entitlements. These are operational counts, not paid invoices, sales totals, revenue or a transactionally frozen snapshot across all services.

The existing `POST /api/v1/platform/plans` now has an Angular form for code/name, locations/domains/member limits and explicit module selection. Creating a plan does not move current tenant subscriptions. A lost response keeps the attempted code visible and asks the administrator to refresh/check that code before retrying; the backend's unique plan code prevents duplicate definitions. Platform audit history uses the existing paged `GET /api/v1/platform/audit`.

## Health checks ka meaning

`GET /api/v1/platform/service-health` checks a fixed set of fourteen services plus Gateway. It does not accept URLs from the caller. The operator supplies base URLs using `PlatformHealth:Services:{Service}`. The local launcher supplies the corresponding localhost ports for all fifteen names without starting additional services. Docker/production need explicit addresses for their network; unconfigured targets stay `NotConfigured`.

Example server configuration, without credentials:

```json
{"PlatformHealth":{"Services":{"Catalog":"http://localhost:5185/","ApiGateway":"http://localhost:5180/"}}}
```

Each probe requests only `/health/ready`, waits at most two seconds, reads response headers and uses at most four concurrent requests. Redirects, cookies and proxies are disabled, and no user bearer is forwarded. Bases must have no credentials, query, fragment or path. HTTPS is required outside Development; Development HTTP is limited to loopback, the known service aliases and `host.docker.internal`. Invalid targets are labelled `InvalidConfiguration` without exposing their values.

`Ready` means that endpoint returned a successful response. `Unavailable` includes non-success, network and timeout failures. It does not mean a stopped local service lost data. A successful Gateway readiness check means its HTTP proxy process/configuration is available; it does not prove every downstream service or checkout. The browser shows sample times and keeps a labelled old result when refresh fails. Health probing is limited to six requests/minute per caller, with no automatic polling.

Health checks do not start, restart or reconfigure services. They do not provision SQL/OAuth/broker accounts, publish an application or perform a checkout. SQL Server for this laptop was verified at `tcp:localhost,1433`, named instance `MSSQL22`; the separate stopped default `MSSQLSERVER` is not the project endpoint.

## Verification and limits

Isolated Tenant SQL/API cases cover administrator authorization, current-term counts, a non-UTC timestamp, audit persistence and private-field exclusion. A controlled HTTP handler covers ready/unavailable/redirect/error/invalid/unconfigured targets, no bearer/cookie forwarding and the concurrency limit. Gateway checks include the direct readiness endpoint while downstream/internal health routes remain unexposed through commerce routes. Angular cases cover actual aggregate values, stale/error health samples and uncertain plan writes.

These views use the existing Tenant tables and plan/audit APIs. Native Chrome verified administrator sign-in, business analytics, the fifteen-service readiness table, audit history and the mobile layout. Fourteen services were ready in that sample. The factory API was outside the default native group. The browser verification exercised audited reads and preserved existing plans and subscriptions.
