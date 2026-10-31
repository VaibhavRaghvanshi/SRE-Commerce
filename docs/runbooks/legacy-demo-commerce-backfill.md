# Legacy Demo: Catalog, Inventory and Pricing backfill

Yeh approved development tenant ke liye non-destructive migration hai. Original SQLite aur old SQL database preserve hote hain. Har service sirf apna SQL database write karti hai; Catalog se published SKU authority HTTP par verify hoti hai.

## Source meaning

Legacy OrdersController aur product UI `Price - Discount` calculate karte hain: Discount fixed per-unit rupees hai. Import MRP = Price, selling price = Price minus Discount rakhta hai. Source mein cost/tax metadata nahi hai: cost null rahegi. Dedicated `LEGACY_DEMO_NO_TAX_METADATA` zero-rate policy **sirf development adapter** hai; yeh kisi legal tax exemption ka claim nahi hai. Existing test tax policies aur price lists overwrite nahi hote.

The verified source contains 1,000 products, 76,202 units, MRP sum 42,273,355 INR and selling-price sum 40,873,382 INR (each product counted once). No source discount exceeds its numeric price. SQLite stores these decimal values as text, so numeric reconciliation converts them explicitly.

## Commands

From the repository root, with the verified local SDK, reviewed SQL migrations applied, and native services available:

```powershell
./scripts/Start-CommerceDevelopment.ps1
node scripts/import-legacy-demo-commerce.mjs
node scripts/import-legacy-demo-commerce.mjs --apply
# Explicitly replay every recorded batch to check stable identities:
node scripts/import-legacy-demo-commerce.mjs --apply --verify-replay
```

Without `--apply`, only read-only source/backup verification runs. The original source hash must match the approved baseline; backup integrity and logical product rows are compared. SQLite backup files can have different header bytes, so their file hashes are not assumed equal.

The importer signs in through real local PKCE using the dedicated Legacy Demo owner's existing private user-secrets. It keeps access tokens only in memory and never prints credentials. Ignored `artifacts/legacy-demo-commerce-import.json` journals exact requests **before** sending them. Interrupted runs reuse those requests and keys; do not delete the journal to force a new import.

## API contracts

- Catalog `POST /api/v1/tenants/{tenant}/catalog/publish-batch`: `{ key, source, products: [{ productId, version }] }`, 1–100 distinct products. Manager authorization, rowversions, normal publication validation, immutable snapshots/outbox and one serializable transaction. Exact retries return original product IDs without advancing revisions; reused key with different payload returns 409.
- Catalog `GET .../catalog/published-skus?ids=...`: Staff-only bounded published SKU metadata, including authorized B2B products. Draft-only, hidden, archived and inactive-category/brand SKUs are excluded. This endpoint is not a public catalog enumeration API.
- Inventory `POST .../inventory/opening-imports`: `{ key, source, warehouseId, lot, rows: [{ productId, variantId, quantity }] }`, maximum 100 distinct SKUs. Manager-only, verified live Catalog identities, immutable operation ledger and atomic receipt movements. Zero quantity creates a tracked SKU with no fabricated receipt. Already tracked identities require explicit versioned stock changes rather than another opening import. Exact retries never add stock twice.
- Pricing `POST .../pricing/imports`: `{ key, source, prices: [PriceRequest] }`, maximum 100 distinct list/SKU/tier combinations. Manager-only, published Catalog authority, same amount/tax/list validation as individual prices, owned immutable PricingImports provenance and price history. Imports create new tiers; they cannot silently overwrite existing prices.

Tenant identity comes from current Identity/Tenant authorization, never a client header or the source file. Keys are tenant scoped and action-separated in payload hashes. All batches remain audited; unchanged retries can succeed even after source entities change, without repeating writes.

## Verification

The importer reads every imported price tier and opening lot back through owning APIs, verifies each source-to-new identity, MRP/selling/cost, tracked zero stock, per-SKU quantity and unchanged original hash. Baseline SQLite has three orders; order-history migration belongs to Order and is not performed by this script. Browser smoke fixtures have separate IDs/lots/lists and remain outside source reconciliation.

Before/after verification commands:

```powershell
./scripts/Verify-CatalogBackup.ps1
./scripts/Verify-InventoryBackup.ps1
./scripts/Verify-PricingBackup.ps1
```

Each script makes a COPY_ONLY checksum backup, verifies it, restores to a new isolated database, runs CHECKDB and makes that restore read-only. Existing databases and backups are preserved.
