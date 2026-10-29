# Tenant API v1

Development origin `http://localhost:5082`; production HTTPS. All `/api/v1` routes require a current Identity bearer session with **identity_api + tenant_api** scopes and `srecommerce.tenant` audience. Identity validates token/signature/session; Tenant obtains the current access context over a bounded trusted HTTP call. Identity outage is 503; invalid/revoked/wrong-scope bearer is 401. `X-Tenant-Id`/role headers do not select authorization.

Pending onboarding is owned by the registering BusinessOwner. Active business operations require a selected active Owner/Manager membership; Staff has read access. Customers/B2B buyers cannot use business-management APIs. Current stored SuperAdmin can perform explicitly audited platform operations. A registering owner without a selected membership cannot manage the active tenant; obtain a new PKCE grant with `tenant_id` after activation.

Every tenant mutation includes base64 SQL rowversion as `version`. Success returns the new TenantView/version; stale/concurrent writes return 409. Missing/invalid input is 400; denied operation 403; inaccessible tenant 404. A 503 does not authorize client-side fallback access. Reload state before retrying a 409. No physical delete endpoints exist.

| Method | Route under /api/v1 | Purpose |
| --- | --- | --- |
| GET | business-types | Active built-in business-type catalog |
| GET | plans | Active immutable plan versions and feature/limit metadata |
| POST | platform/plans | SuperAdmin creates a new plan code; no billing/charges |
| GET | platform/audit?page=1 | SuperAdmin reads bounded global catalog/platform audit history |
| GET | tenants?page=1 | Selected accessible tenant plus own pending onboarding; platform list audited |
| POST | tenants | BusinessOwner registration; GUID `Idempotency-Key` header required |
| GET | tenants/{id} | Accessible tenant metadata/version |
| POST | tenants/{id}/submit | Draft to PendingApproval |
| POST | platform/tenants/{id}/activate | Provision Identity owner via delegated admin, then activate/resume |
| POST | platform/tenants/{id}/suspend | Active to Suspended, reason required |
| POST | platform/tenants/{id}/archive | Suspended to Archived; data retained |
| GET/PUT | tenants/{id}/settings | Currency/culture/timezone/display-name/logo metadata |
| GET | tenants/{id}/entitlements | Effective subscription/state/module flags and declared quotas |
| PUT | tenants/{id}/modules/{module} | Owner/Manager enable/disable an entitled supported module |
| PUT | platform/tenants/{id}/subscription | SuperAdmin assigns a plan/validity/state; refuses usage-incompatible downgrade |
| GET/POST | tenants/{id}/locations | Read/create business locations within plan quota |
| POST | tenants/{id}/locations/{locationId}/deactivate | Preserve inactive location record |
| GET/POST | tenants/{id}/domains | Read domains or create/renew a pending TXT challenge |
| POST | tenants/{id}/domains/{domainId}/verify | Verify unexpired token and DNS TXT; optionally set primary |
| POST | tenants/{id}/domains/{domainId}/deactivate | Disable domain routing metadata, retain record |
| GET | tenants/{id}/audit?page=1 | Owner/Manager or audited platform access; 50 events/page |

Registration fields: name, slug, businessTypeCode, optional customBusinessType (required only for `custom`). Supported types: spices, clothing, grocery, general-store, custom. Repeating the same owner/key/payload returns the existing registration; changing payload with the key returns 409. Slug is globally unique. Concurrent first registration may return a retryable 409; retry with the same key.

Tenant status enum JSON: Draft=0, PendingApproval=1, Active=2, Suspended=3, Archived=4. Subscription status: Trial=0, Active=1, Cancelled=2. Date values are UTC offsets. Trial/active must be inside `[startsAt, endsAt)` for entitlements. Shared database strategy is implemented; dedicated database provisioning is future work and cannot be selected via API.

Settings body: version, currency (three uppercase letters), culture, timeZone, displayName, optional HTTPS logoUrl. Appearance tokens/themes are outside this service. Locations body: version, name, address, two-uppercase-letter countryCode. Plan creation: code, name, maxLocations (1–100), maxDomains (0–50), maxUsers (1–100000), modules (unique supported names). Plans are immutable after creation: publish a new code rather than silently changing existing subscriptions.

Modules: themes, storefront, catalog, inventory, pricing, cart, orders, payments, shipping, notifications, manufacturing, discovery. Enabling a module records configuration; it does not create/deploy the future service. Starter allows storefront/catalog/cart/orders and one location/domain; growth allows all listed modules, five locations and three domains. These are development plan metadata, not paid offerings.

Domain challenge response returns recordName and token once (also returned on explicit renewal), with seven-day expiry and updated version. Publish TXT `srecommerce-verification=<token>` at recordName, then submit version/token/makePrimary. Domain lists/audits omit token/hash. Hosts are IDN-normalized; URLs, wildcards, IPs and invalid labels are rejected. No Host-header routing or gateway/domain certificate installation is implemented. Existing domain reservations are preserved on deactivation; transfer/release needs a separate reviewed workflow.

Effective modules require Active tenant, an effective non-cancelled subscription, an active plan, an enabled plan feature and tenant configuration. Tenant enforces location/domain/module constraints; `maxUsers` is a declaration for the Identity-owned membership workflow to enforce through a future consumption contract. No cross-database membership count is inferred.

Health `/health/live` and `/health/ready`; readiness checks Tenant SQL schema only. It does not assert Identity availability for business requests. Development OpenAPI `/openapi/v1.json`.
