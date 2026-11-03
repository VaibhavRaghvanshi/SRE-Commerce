# Complete backend and SQL phase

**Current folder rule:** per owner's GrowthStack reference, each service lives directly under `SRECommerce.<Service>.API/` with its own `SRECommerce.<Service>.API.sln` and five projects. Angular is `SRECommerce.UI`. Implemented source is organized into Controllers/DTOs/Interfaces/Entities/Data/Services and applicable Repositories. Complete relocation checks before resuming new business features.

**Latest owner instruction (2026-10-31) takes precedence:** develop backend and Angular together, using the same five-project service structure, and push tested checkpoints to the existing GitHub migration branch without repeated approval. This supersedes the earlier backend-first/Angular gate and separate push approval recorded below. Keep existing data and secrets private; destructive changes remain outside routine development. New runtime credential scripts remain unexecuted at this checkpoint. Focus first on an integrated Catalog → Inventory → Pricing → Cart → Order flow; report remaining scope honestly.

**2026-11-01 update:** focus on code and continue GitHub source pushes; defer application publishing/deployment until implementation is complete. Eight service backends are established, Order is active, and Payment/Shipping/Notification/Manufacturing/Discovery remain pending. Catalog/Inventory/Pricing/Cart have their current Angular flows; Identity and Order have partial UI integration. See the current frontend matrix in [migration progress](../migration-progress.md).

Backend, SQL and Angular now move together. GitHub source pushes are approved. Publishing or deploying the application and provisioning the deferred runtime credentials remain separate from routine development.

The owner also selected current SQLite data (1,000 products / 3 orders) for non-destructive backfill into an explicit **Legacy Demo** tenant. Preserve both original stores; do not merge the divergent older SQL product catalog into it. Imported identities retain their reviewed mappings and unconfirmed email state; no legacy administrator becomes platform SuperAdmin. Dedicated demo ownership and customer memberships will be explicit and audited.

## Delivery matrix

| Service | Database | Required backend slice | State |
| --- | --- | --- | --- |
| Identity | SRECommerce_IdentityDB | Existing PKCE/users/MFA; bounded machine identity and commerce scopes/contracts | Native 26 tests; Angular public PKCE client/browser flow verified; new confidential provisioning held |
| Tenant | SRECommerce_TenantDB | Lifecycle; member entitlements, public registry, quota/lifecycle integration | 28 tests; registry/entitlements used by real Catalog/browser; earlier Docker verified |
| Theme | SRECommerce_ThemeDB | Validated accessible tokens/presets, draft/preview/publish/version/rollback, cached/default fallback | Native verified: 17 full-suite tests + SQL-outage test; schema/backup/restore verified; Docker runtime pending credentials |
| Storefront | SRECommerce_StorefrontDB | Sections/navigation/banners/field visibility, layout versions/preview/publish, fallback | 25 tests; main schema/backup/restore verified; Docker runtime pending |
| Catalog | SRECommerce_CatalogDB | Categories/brands, typed attributes, products/SKUs/media/bundles/collections, publish/import | Native 21 tests; atomic batch publication and published staff/customer/B2B SKU contracts, main SQL migration/backup and initial Angular flow verified; media upload and Docker runtime pending |
| Inventory | SRECommerce_InventoryDB | Warehouses/branches, balances/ledger/batches, transactional reservations/releases, purchasing | Native 19 tests; owned SQL migration/backup/restore, Angular/browser stock flow and 1,000-SKU/76,202-unit source backfill verified; broker notifications and Docker runtime pending |
| Pricing | SRECommerce_PricingDB | Price lists/groups/tiers, promotions/coupons, authoritative quotes/tax metadata and redemption | Native 33 tests; three owned migrations/backup/restore, scoped OAuth tests, Angular/browser quotes and 1,000 source-price tiers verified; production tax/provider and Docker runtime pending |
| Cart | SRECommerce_CartDB | Durable guest/customer carts, merge, quantities/expiry and authoritative checkout preparation | 23 regression tests pass; main schema and checkout preparation verified. Angular cart and checkout screens added |
| Order | SRECommerce_OrderDB | Idempotent checkout saga, snapshots/status, cancellation/returns/exchanges and compensation | Main schema, three legacy orders and real Payment/Shipping integration tested. Angular checkout, customer history, merchant fulfillment/returns/exchanges and checkout recovery screens added |
| Payment | SRECommerce_PaymentDB | Sandbox provider, verified/idempotent callbacks, refunds and reconciliation; no real money/card storage | 14 real SQL/API and signature cases pass. Main schema including zero-charge orders and restore verified. Angular Payments workspace added |
| Shipping | SRECommerce_ShippingDB | Zones/methods/quotes, pickup/local delivery, partial shipments/tracking/return pickup | 14 SQL/API tests plus two focused delivery-region cases pass; 13-table main schema/backup/restore verified. Angular Shipping workspace and customer tracking added |
| Notification | SRECommerce_NotificationDB | Templates/preferences/jobs/retries/logs; local Mailpit, mock SMS/WhatsApp adapters | SQL migration/restore and Order proof/replay/delivery test pass. Angular templates, logs and preferences added. Broker, low-stock alerts and live runtime delivery verification pending |
| Manufacturing | SRECommerce_ManufacturingDB | Optional BOM/stages/materials/production batches/consumption/output/cost/quality | SQL migration/restore and actual Inventory integration test pass, including lost receipt response. Angular production workspace added |
| Discovery | SRECommerce_DiscoveryDB | Tenant search projections, filters/facets/suggestions, reviews/moderation/verified purchase | SQL migration/restore and Full-Text tenant isolation/moderation test pass. Angular search and reviews added. Index refresh is manual; live runtime review verification needs scoped client |
| ApiGateway | None | YARP allowlisted routes, correlation/auth/rate/CORS/timeouts; no public internal endpoints | 15 route/proxy tests pass. Production deployment and end-to-end telemetry pending |

Each remaining service has exactly API, Application, Domain, Infrastructure and Tests projects under `SRECommerce.{Service}.API`. Domain/application boundaries and business models stay service-owned. Building blocks contain transport contracts, scoped authorization, auditing/outbox/inbox plumbing, cache/resilience and hosting helpers, never a shared business DbContext or cross-service SQL connection.

Owner confirmed the same source layout for every remaining service: controllers live in the API project's `Controllers/` directory, with five separate layer/test projects. Create each service with substantive implementation and verified progress; generated bin/obj output is not source completion.

## Integration and security

Owner decision (2026-10-30): **new credential provisioning is on hold**. Continue source/API implementation and native Windows-auth SQL migrations/tests. Do not execute `Initialize-BackendAccess.ps1`, `Initialize-BackendInfrastructure.ps1` or register new runtime OAuth clients/accounts until a later explicit approval. Existing previously approved Identity/Tenant credentials remain usable. Docker integration requiring new service SQL logins and broker/cache authentication stays pending, rather than being reported as verified.

User calls use Identity PKCE and live account/membership checks, `commerce_api` plus Identity/Tenant context scopes. New APIs enforce current Tenant module entitlements and actor permissions; presentation snapshots never authorize price/stock/payment. A public Tenant registry exposes only published business metadata/capabilities. Public Theme/layout use last valid published snapshots and bundled defaults through outages.

Recovery workers use separately approved confidential client-credentials identities with a narrow source-to-target scope matrix. No bearer token is persisted in a saga/outbox. User tokens cannot invoke internal service routes, and machine tokens cannot impersonate human profiles. Internal paths are blocked at the gateway. Privileged support operations require a reason and durable audit.

RabbitMQ is justified by projection/notification/checkout integration: own-service SQL outbox, confirms, versioned envelopes, own queues, idempotent inbox, bounded retry and DLQ. Valkey is optional/non-authoritative for public snapshots and search summaries; use tenant/service-prefixed keys, TTL and safe misses/outages. Kafka remains deferred. Broker accounts, SQL runtime logins and OAuth credentials are generated privately only after the provisioning gate.

SQL Server Developer and Full-Text Search are installed (`IsFullTextInstalled=1`). Discovery will use tenant-scoped SQL Full-Text indexes with documented query constraints. No OpenSearch or replacement SQL instance is needed. Schema creation/migrations and backfill occur one service at a time; data is preserved and isolated restores are verified. No destructive cleanup is authorized.

## Release acceptance

For all fourteen services: substantive business APIs, owned SQL schema/indexes/concurrency, reviewed migrations, isolated real SQL tests, tenant/security/failure tests, health/OpenAPI, Docker build/start verification, contract documentation and backup/backfill evidence. Verify successful checkout, payment failure/compensation, duplicate events, broker/cache outages, presentation fallback and source count/hash preservation. Matrix rows remain pending until evidence exists; an empty project or generic CRUD scaffold is not completion.

Run small dependency groups on the roughly 8 GiB laptop and serialize heavy builds/tests. Do not start fourteen APIs/heavy infrastructure together. Keep production providers, actual billing/money movement, paid messaging and production deployment outside local verification. Optional future POS/loyalty/gift-card integrations must not be claimed as implemented solely through a module flag.
