# Incremental migration plan

Date: 2026-10-28. Status: proposed; WAITING_FOR_USER_APPROVAL.

## Proposed next implementation task: baseline stabilization

This is one prerequisite phase, not an approval to implement fourteen services.

1. Locate or recover the original Git checkout and verify its history/remote. Capture the current file changes safely. Create `migration/saas-modernization` there, without resetting files or replacing history. If this is the only surviving copy, obtain the owner's decision on history recovery before initializing anything.
2. Preserve SQLite, SQL Server SRECommerceDb and uploaded media. Review and approve backup destinations, take consistent backups and test restore into isolated development copies. Never use a live SQLite file copy while writes are active; use its backup mechanism or stop the writer safely.
3. Establish repeatable isolated .NET/Angular build and smoke baselines. Pin an installed stable SDK for the legacy build; assess the stable .NET 10 SDK setup separately. Fix the two stale test imports and assertions against the actual components; ensure the dashboard tests target the routed component. No new frontend module is included.
4. Correct and validate JWT lifetime binding and claim handling; lock down user-management privilege escalation, uploads, development seeding and secret configuration. Add focused authorization and token regression tests. Do not rotate shared/live credentials without coordinating the affected environment.
5. Align the documented API launch port and dev proxy (5027 versus 5297). Verify catalog, home, login/me, staff-denial checks, order creation and order history using disposable data.
6. Record the verified baseline, unresolved failures and a data reconciliation decision. Do not change the database provider or copy data into the target services in this phase.
7. Stop and report files, database/API changes, test commands/results and the next single proposed task. Ask for the next approval.

Deliverable: a reproducible, backed-up, tested monolith and an agreed data source, ready for the first approved microservice. Estimated 3-6 engineering days excluding recovery/install blockers.

## Service ownership and exact naming

For every row, create these five projects only when that service is individually approved:

- `SRECommerce.{Service}.API`
- `SRECommerce.{Service}.Application`
- `SRECommerce.{Service}.Domain`
- `SRECommerce.{Service}.Infrastructure`
- `SRECommerce.{Service}.Tests`

For Identity, these expand to `SRECommerce.Identity.API`, `SRECommerce.Identity.Application`, `SRECommerce.Identity.Domain`, `SRECommerce.Identity.Infrastructure` and `SRECommerce.Identity.Tests`. The same exact expansion applies to each identifier below. API references Application and Infrastructure for composition; Application references Domain; Infrastructure references Application and Domain; Domain stays independent; Tests are never deployed. Keep the existing four projects during migration. Proposed folders are `SRECommerce.{Service}.API/`; do not bulk move the monolith to satisfy a diagram.

| Service identifier | Logical SQL Server database | Ownership and current input |
| --- | --- | --- |
| Identity | SRECommerce_IdentityDB | Users, roles, permissions, memberships, sessions, token records and security audits; input: current Users/auth |
| Tenant | SRECommerce_TenantDB | Businesses, onboarding, types, domains, locations, settings, plans, subscriptions, entitlements; new domain |
| Theme | SRECommerce_ThemeDB | Visual tokens, presets, tenant assignments, versions, publication and rollback; map legacy colors, not product data |
| Storefront | SRECommerce_StorefrontDB | Pages, sections, menus, banners, placements, layout versions/publications; input: HomeSections/Items/Banners |
| Catalog | SRECommerce_CatalogDB | Products, category tree, brands, attributes, variants/SKUs, media, bundles, publishing; input: product/category metadata |
| Inventory | SRECommerce_InventoryDB | Warehouses, balances, reservations, movements, batches, suppliers and purchases; input: legacy StockQty reconciliation |
| Pricing | SRECommerce_PricingDB | Prices, tax/currency policies, price groups, discounts, coupons and promotions; input: Price/Discount |
| Cart | SRECommerce_CartDB | Durable carts, items and cart sessions; browser cart is import input requiring validation |
| Order | SRECommerce_OrderDB | Orders, immutable item/price/address snapshots, lifecycle, returns/exchanges, checkout coordination; input: Orders/Items |
| Payment | SRECommerce_PaymentDB | Provider attempts, transactions, verified webhooks, refunds and reconciliation; sandbox only initially |
| Shipping | SRECommerce_ShippingDB | Delivery zones/methods/rules, shipments and tracking; replace the hardcoded shipping fee |
| Notification | SRECommerce_NotificationDB | Templates, preferences, jobs, delivery attempts and logs; local email capture initially |
| Manufacturing | SRECommerce_ManufacturingDB | Optional BOM, raw materials, production stages, consumption, output and quality checks |
| Discovery | SRECommerce_DiscoveryDB | Search projections/index metadata and reviews/moderation; catalog events and verified order evidence |

Gateway: separate `SRECommerce.ApiGateway` using YARP, with no business database. Building blocks may contain stable contracts, tenant/context primitives, outbox plumbing and observability helpers; never a shared business DbContext or shared entity model.

## Sequence and approval gates

1. Baseline stabilization, then stop.
2. Identity backend/database/migrations/tests, then stop. Use ASP.NET Core Identity and a reviewed OAuth2/OIDC approach; do not label custom login JWT issuance a complete authorization server. Tenant membership stores tenant references without calling a not-yet-built Tenant database. Full tenant lifecycle integration remains explicit pending work.
3. Tenant, then stop. Reconcile the tenant registry and membership lifecycle through contracts/events; reject suspended membership and cross-tenant requests.
4. Gateway, then stop. Route only approved public endpoints; preserve legacy routes through a compatibility adapter. Add authorization/rate limiting at services as well as the edge.
5. Theme, then stop; Storefront, then stop. Published snapshots and bundled business/global defaults must precede replacing the old homepage. Approve any Angular integration as its own module.
6. Catalog, Inventory, Pricing, Cart and Order in dependency order, each with its own approval. Keep existing checkout on the legacy path until all authoritative dependencies and compensation are verified. No temporary cross-service SQL joins.
7. Payment and Shipping (each gated), then enable integrated checkout only after sandbox/failure testing. Notification may be prioritized earlier by approval for a specific required workflow; do not build it implicitly inside Identity.
8. Discovery and optional Manufacturing, each gated. Add frontend modules at explicit checkpoints, not a simultaneous replacement of the three application areas.

## Data migration protocol per service

1. Recover historical migration source where possible. Inventory actual schemas instead of assuming current entities match old SQL tables.
2. Use consistent backups and isolated restore tests; reconcile both stores with the owner. SQLite's larger sample catalog does not prove every record is authoritative.
3. Produce source-to-target field mappings, stable legacy-ID mapping, a single explicit legacy tenant assignment, count/checksum expectations, duplicate/orphan policy and retained upload paths. Never assign all customers or roles to arbitrary new tenants silently.
4. Create only the approved target database with a least-privileged service identity. Generate reviewed SQL Server migrations for that schema. Do not replay SQLite provider migrations onto SQL Server or baseline the old SQL database as though it were empty.
5. Apply migrations to an isolated development copy; verify constraints, tenant-composite indexes, rowversion/concurrency, amounts/rounding, Unicode/collation and timestamps.
6. Rehearse resumable, idempotent backfill. Preserve BCrypt legacy hashes using an explicitly identified legacy verifier that rehashes with ASP.NET Core Identity on successful login, or require reset; never pass BCrypt strings to the default Identity verifier as if they were native hashes. Test and document the selected transition.
7. For the small initial dataset, prefer a planned write pause and final delta reconciliation over an untested dual-write system. If zero-downtime change capture becomes necessary, design and test it first. Transfer ownership with one authoritative writer.
8. Shadow compare authorized reads and verify tenant isolation before route cutover. Keep price/stock/payment checks live; stale presentation snapshots never authorize transactions.
9. Keep old data and routes available for the agreed rollback window. After target writes start, rollback needs a tested reverse-delta/reconciliation plan or forward recovery; switching back alone can lose new writes.
10. Remove legacy columns/tables/code only in a later separately approved contract phase. No drop, reset, EnsureDeleted or volume deletion is authorized by this plan.

## Service completion gate

For the approved service: domain/ERD, schema/constraints/indexes, Clean Architecture projects, validated versioned API contracts, authentication/tenant authorization, error handling, service health/OpenAPI, reviewed migration scripts, development migration/restore verification, unit/integration/contract/security/tenant tests, Dockerfile, resource notes, outage demonstration, ADR/runbook and exact commands/results. Anything unverified stays pending. Stop after the service and request the user's next choice using the supplied prompt's completion wording.
