# Inventory service

Solution: SRECommerce.Inventory.API/SRECommerce.Inventory.API.sln. API, Application, Domain, Infrastructure and Tests are separate projects. Application use cases call IInventoryRepository and IInventoryAccess; the SQL repository, HTTP integrations and expiry worker live in Infrastructure. Runtime projects reference shared contracts, never another service's business projects/database.

Owned database: SRECommerce_InventoryDB, migration 20261031132333_InitialInventory. Tables: Branches, Warehouses, StockItems, InventoryBatches, InventoryBalances, InventoryMovements, StockOperations, StockReservations, StockReservationLines, Suppliers, PurchaseOrders, PurchaseOrderLines, AuditEntries, IntegrationOutbox, IntegrationInbox and EF migration history. Tenant composite keys/FKs, rowversions, quantity checks and scoped query/write guards protect data. No foreign-service SQL reference exists.

## Authorization and routes

Protected base: /api/v1/tenants/{tenant}/inventory; gateway: /commerce/inventory/tenants/{tenant}/inventory. Live Identity account/membership and Tenant module checks enforce Staff reads and Owner/Manager mutations. Purchasing and movement reads require Manager. Platform support requires a reason and durable audit. A tenant/role header never grants authority.

| Route | Methods / behavior |
| --- | --- |
| lookups | GET owned branches, warehouses, registered SKUs and suppliers |
| branches, branches/{id} | POST/PUT with version; active warehouses block branch deactivation |
| warehouses, warehouses/{id} | POST/PUT with version; stocked warehouses cannot be deactivated or moved |
| items | POST productId, variantId, reorderLevel, optional version; Catalog published snapshot verifies SKU identity, including separately authorized B2B products |
| balances | GET; optional warehouse/variant filters; usable on-hand, reserved, damaged and available quantity per lot |
| movements | GET latest 100/page, optional balance filter; immutable ledger |
| stock-changes | POST idempotent opening receipt, signed adjustment, damage, return-to-sale or physical reconciliation |
| reservations | POST bounded idempotent hold with variant quantities; GET /{id} |
| reservations/{id}/commit | POST version; deducts usable physical stock and clears held units |
| reservations/{id}/release | POST version; clears held units without consuming stock |
| reservations/expire | POST explicitly processes due holds for this tenant |
| suppliers, suppliers/{id} | POST/PUT; versioned active state and optional contact email |
| purchases | POST purchase draft; GET latest 50/page |
| purchases/{id}/submit or cancel | POST version; partial receipts remain preserved on cancellation |
| purchases/{id}/receive | POST idempotent partial receipt with operation ID, version, line quantities, lot and expiry |

Anonymous /api/v1/public/tenants/{tenant}/inventory/availability?variants=<guid> accepts up to 100 distinct repeated variant parameters. Only live public Catalog variants in an active inventory-enabled tenant expose quantities. Hidden/archived/B2B-only/unregistered variants return tracked:false. No costs, suppliers, warehouses or internal ledger metadata are public. Required Catalog/Tenant/SQL outages return 503; no stale stock fallback authorizes checkout.

Internal /internal/v1/tenants/{tenant}/inventory/reservations and /{id}/{commit|release} require a narrow machine identity from Cart, Order or Manufacturing plus current Tenant module state. Human tokens cannot call them and the gateway blocks internal paths. Live confidential-client provisioning and Order/Cart integration remain tracked separately.

## Stock rules

Quantities use decimal(18,3). Usable on-hand includes reserved units; damaged units are separate. Available = usable on-hand minus reserved, only for non-expired lots in active warehouses/branches. Receipts preserve immutable lot identity/expiry; changing expiry on an existing lot is rejected. Catalog owns SKU/barcode identity; Inventory stores a verified local reference and no cross-database FK.

Reservations use a serializable SQL transaction, rowversions and check constraints to prevent overselling. Allocation is FEFO among lots that remain valid through the hold expiry. Holds last 30 seconds to one hour. Duplicate IDs with identical payloads replay the recorded reservation; changed payloads conflict. Release/commit are terminal and idempotent. Expired replay/commit requests release the hold as Expired instead of consuming stock.

A bounded worker processes due reservations every 30 seconds using fresh tenant-bound maintenance scopes. One tenant failure does not stop remaining tenants; the next pass retries. Until a release commits successfully, the hold continues to reduce availability, avoiding overstated stock.

Every stock mutation records immutable movement deltas, an actor audit and an integration outbox entry in the same transaction. Opening receipts and purchase receipts retain stable operation results for exact retries. Subsequent mutations require a freshly loaded rowversion. Damage/reconciliation cannot remove reserved units or create negative balances. Purchase receipts cannot exceed ordered quantities; cost data never sets a public selling price.

HTTP 400 = invalid input/version; 401 = no current actor; 403 = role/module denied; 404 = missing tenant-owned resource; 409 = stale version, idempotency conflict or insufficient stock; 503 = required dependency unavailable.

## Angular and local operation

/workspace/{tenant}/inventory provides branch/warehouse setup, published SKU registration, opening receipts, damage/adjustment/reconciliation, manual holds/release/commit, supplier setup, purchase submission/partial receipt and ledger viewing. Receipt/reservation retries preserve operation identity. Public product detail reads real Inventory availability and keeps price/checkout unavailable until Pricing/Cart integration is ready.

The native launcher starts Inventory on 5186 with Identity 5181, Tenant 5182, Catalog 5185 and Gateway 5180. Development uses Windows-authenticated installed SQL Server. Startup does not migrate or seed. The explicit scripts/prepare-legacy-demo-inventory.mjs --apply command enables the local demo's inventory module through the development growth plan and creates one labeled branch/warehouse; no real billing occurs.

Tests cover concurrent overselling, ledger conservation, idempotency, FEFO/expiry, damage/return/reconciliation, partial purchasing, tenant boundaries, module disablement, dependency outages, published-SKU visibility, migrations/readiness/OpenAPI and human/internal separation. Verification results are in migration progress.

Remaining scope: 1,000-product stock backfill, large-tenant report pagination/exports, receiving returned customer orders, broker-driven low-stock notification delivery, confidential machine integration and Docker runtime acceptance. Low-stock flags and outbox rows are implemented; delivered notifications are not claimed.

## Approved Legacy Demo batch backfill

See [the verified source migration runbook](../runbooks/legacy-demo-commerce-backfill.md). Catalog 21, Inventory 19 and Pricing 33 affected-service tests passed with real SQL/Identity/Tenant integration. The approved 1,000-product source is published with 1,000 owned price tiers and 76,202 opening-stock units, reconciled per SKU through service APIs. Source prices preserve fixed per-unit discounts and unknown cost; no original database was replaced. The dedicated zero-rate policy is explicitly development-only because source tax metadata is absent. Broker delivery, source order migration and production tax configuration remain separate work.
