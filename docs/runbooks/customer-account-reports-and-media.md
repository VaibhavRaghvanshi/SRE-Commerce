# Customer accounts, sales reports and product images

The modern UI continues under `SRECommerce.UI`; these features use the existing Cart, Order and Catalog APIs. They do not introduce a shared database or modify legacy SQLite data.

## Customer account

Open `/stores/{slug}/account`. Normal Identity code+PKCE sign-in selects that store. Customers can keep ten versioned addresses, up to 100 wishlist entries, and 25 recent products. The API derives the customer ID from live Identity context; neither a request body nor a support header selects another customer's account. Owner/staff accounts can use their own personal account when they have a current membership.

Authenticated routes, relative to `/api/v1/tenants/{tenant}/cart/account`:

| Method | Route | Behavior |
| --- | --- | --- |
| GET | `/addresses` | Current customer's active addresses |
| PUT | `/addresses/{id}` | Create with a new client-generated ID; update with `version` |
| POST | `/addresses/{id}/archive` | Versioned removal from the active address book; history retained |
| GET | `/products/0?page=1` | Wishlist, 25 items per page, live Catalog visibility |
| PUT | `/products/0/{product}` | Explicit save or versioned removal using `{active, version}` |
| GET | `/products/1` | Most recent account product views |
| PUT | `/products/1/{product}` | Record a currently public product view |
| POST | `/recently-viewed/clear` | Clear the active recent list |

The Angular gateway prefix is `/commerce/cart/tenants/{tenant}/cart/account`. Checkout offers the address selector only to the signed-in customer and rechecks delivery options after selection. Saving an address does not create an order. Removing one does not rewrite immutable past order snapshots.

Guest recent history contains only up to 25 product IDs in a tenant-specific localStorage key. Names, links and eligibility are fetched from the live public Catalog, so an archived product is not shown from a cached snapshot. Account recent history stays in SQL. Contact details and bearer tokens are never placed in this history key. Guest history is not silently merged into an account.

## Merchant sales overview

Open `/workspace/{tenant}/reports`, or follow **Sales overview** from Orders. Owners/managers can read the report; support requires the existing audited reason flow. The endpoint is `GET /api/v1/tenants/{tenant}/orders/reports/sales?from=2026-11-01&through=2026-11-04&currency=INR&includeHistorical=false`.

Dates are inclusive UTC placement dates, limited to 366 days ending today or earlier. A report contains one currency. Imported history is excluded by default. Aggregation runs in the owning Order database and returns no customer contacts. An additive covering index supports tenant/date queries.

- Accepted value includes current Confirmed through Completed orders, delivery and configured tax.
- Refunds are current cumulative refunds for all orders placed in the selected cohort, including later refunds.
- Captured-state balance uses Captured/PartiallyRefunded/Refunded order states less refunds. Uncollected COD is excluded.
- Top product quantities/value come from accepted order snapshots before later returns.

The report is a live operational view, not a settlement ledger, a profit calculation or an accounting-period refund report. Historical unknown costs/payment details are not invented.

## Product images

The Catalog editor accepts PNG/JPEG uploads up to 750,000 bytes, dimensions up to 8,192 on either axis and at most 16,777,216 pixels. Actual file signatures and dimensions determine the MIME type. SVG, HTML, arbitrary extensions and client file paths cannot select stored content. Each tenant has a 256 MiB managed asset quota and products retain their existing twenty-image limit.

`POST /api/v1/tenants/{tenant}/catalog/media-assets` accepts multipart `file` from a manager and returns an opaque asset ID, URL and dimensions. The editor adds this URL to the current draft. **Save and publish** are still explicit product actions. Cross-tenant managed asset URLs are rejected.

`GET /api/v1/public/tenants/{tenant}/catalog/media/{id}` serves the recorded MIME with `nosniff` only while a current, nonarchived, publicly visible, non-B2B publication references the asset. Merely uploading a file does not publish it. Public files are not served directly from an unrestricted static folder.

Development files live under `%LOCALAPPDATA%\SRECommerce\Media\Catalog\{tenantN}\{assetN}.image`, outside Git, or an explicitly configured absolute `Media:Root`. SQL stores ownership, hash, MIME, size and dimensions. Backup this directory along with CatalogDB; a SQL backup alone does not contain image bytes. Failed metadata commits may leave unreferenced files retained for later reviewed reconciliation. No automatic file deletion occurs. The local adapter is Development-only; production object storage and lifecycle policy remain deployment work.

## Database scripts

Reviewed additive scripts in `docs/database-migrations/`:

- `catalog-product-images.sql`: managed asset metadata.
- `cart-customer-account.sql`: scoped addresses and saved products.
- `order-sales-report-index.sql`: covering date index.

Every script refuses the wrong owning database. Use Windows-auth SQL for local development, for example:

```powershell
sqlcmd -S 'tcp:localhost,1433' -E -C -b -I -d SRECommerce_CartDB -i docs/database-migrations/cart-customer-account.sql
./scripts/Verify-ServiceDatabaseBackup.ps1 -Service Cart
# With upload writers stopped, pair a Catalog SQL backup with the managed files:
./scripts/Verify-ServiceDatabaseBackup.ps1 -Service Catalog
./scripts/Backup-CatalogMedia.ps1
```

Original databases, imported order snapshots and backups remain intact. Runtime SQL/OAuth provisioning and application publishing remain on their existing holds.
