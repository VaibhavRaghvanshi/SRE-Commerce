# Catalog backend and Angular slice

Independent solution: `SRECommerce.Catalog.API/SRECommerce.Catalog.API.sln`, five layer/test projects. Domain owns products, categories/tree, brands, attribute definitions/mappings, SKU variants, media references, bundles, relationships, collections, immutable publications and import provenance. Application `CatalogService` orchestrates through `ICatalogRepository` / `ICatalogAccess`; SQL implementation is in Infrastructure/Repositories. No foreign-service SQL reads.

## Protected resource API

Base `/api/v1/tenants/{tenantId}/catalog`; gateway `/commerce/catalog/tenants/{tenantId}/catalog`. A current selected-tenant Identity PKCE token needs `identity_api tenant_api commerce_api`; live account/membership/module checks enforce Staff reads and Manager/Owner writes. Platform support requires `X-Support-Reason` and a durable audit. Spoofed tenant/role headers grant nothing.

| Resource | Operations |
| --- | --- |
| `lookups` | GET categories/brands/definitions/collections |
| `categories` / `categories/{id}` | POST/PUT; rowversion, slug uniqueness, tree cycle/archival checks |
| `categories/{id}/attributes` | GET effective inherited fields; PUT direct bindings with `?version=` and `{attributeId,required}[]` |
| `brands`, `attributes` | POST and PUT `/{id}`; typed definitions and immutable field code/type |
| `products` / `products/{id}` | POST/GET/PUT drafts; paged/search/category/brand filters |
| `variants/{id}` | GET the published variant's product for authorized Staff; new unpublished draft variants remain absent |
| `products/{id}/publish` | POST `{version}` creates immutable publication and outbox atomically |
| `products/{id}/archive` | POST `{version}` retires visibility/sellability while preserving data/history |
| `collections` / `collections/{id}` | POST/PUT with ordered product IDs, archival and rowversion |
| `export` | GET paged typed drafts (maximum 100/page) |
| `import` | POST `{key,source,products}`; maximum 100 new drafts per atomic import; same key/payload replays IDs, changed payload conflicts |

ProductDraft includes name/slug/category/brand/kind, plain description/SEO metadata, visibility/B2B flags, typed product attributes, specifications, variant options/SKUs/barcodes, ordered media URLs, bundle components and related/upsell/cross-sell references. Prices and stock belong to Pricing/Inventory; Catalog never invents those values. New variant/media IDs are null; existing IDs must belong to this exact tenant/product. Removed children are deactivated, not physically deleted.

Attributes inherit from category ancestors; unknown fields/wrong types fail, required fields are enforced on publication. Variant options describe sellable SKU differences separately. SKU names are canonical uppercase/unique per tenant, including retired SKUs; published SKU identity and product slug/kind are immutable. Bundles cannot nest/self-reference and require published component variants, positive quantities and compatible B2B visibility. Media references allow HTTPS and safe local `/uploads/` or bundled `/assets/Images/` paths. This slice references media; binary image upload/storage lifecycle remains pending.

## Public and internal reads

Anonymous `/api/v1/public/tenants/{tenantId}/catalog/{lookups|products}` and `/products/{slug}` require a live published Tenant/module registry. Query filters operate on immutable publication columns, not newer draft fields. Editing a published draft cannot leak its changed title/category/attributes before republishing. Archived product/category/brand, hidden and B2B-only products are absent from the anonymous catalog. Results are paged and no secret/private price/cost field is included.

Anonymous `/variants?ids=<guid>` returns only the requested variant IDs visible in public published snapshots (maximum 100 repeated IDs). Inventory uses this contract to prevent public stock disclosure for hidden/archived/B2B-only SKUs. Staff variant reads use the published snapshot too; importing a draft-only variant into Inventory is rejected.

Internal `/internal/v1/tenants/{tenantId}/catalog/variants/{id}` requires a narrow machine scope and permitted source service, never a human bearer. The gateway does not expose it. Machine provisioning/live downstream integration remains separately tracked; native SQL/API tests cover human/internal separation.

HTTP 400 = invalid shape/type/version, 401 = missing/current-invalid actor, 403 = role/module denied, 404 = absent tenant-owned/published resource, 409 = stale/duplicate/conflicting state, 503 = required live dependency unavailable. Own SQL serializable mutations, rowversions, tenant composite FKs/filters/write guards and audit/outbox protect state. Startup never migrates/seeds automatically.

## Current Angular integration

`/stores/{slug}`: live published catalog, search/category/brand/collection filters, paging, retry/empty states and product links. `/stores/{slug}/products/{productSlug}`: product metadata, media, variant selection, specifications/attributes. No fake price/stock/add-to-cart approval is shown while those integrations are pending.

`/workspace/{tenantId}/catalog`: first-party OpenID Connect code+PKCE via `oidc-client-ts`, Owner/Manager product draft/edit/publish/archive, categories, typed field definitions/category assignment, variants/media and basic bundle editing. User access/refresh tokens are memory-only; sessionStorage holds short-lived protocol state. Backend permissions remain authoritative. `/commerce/callback` must reach Angular, so development/production resource proxies match only named service prefixes.

The local launcher uses Identity 5181, Tenant 5182, Theme 5183, Storefront 5184, Catalog 5185 and gateway 5180, avoiding prior Docker ports. Angular dev uses 4200; production Node uses 4000 with `COMMERCE_PROXY_TARGET=http://localhost:5180`. Production authority/origin/provider deployment hardening is not claimed by these local profiles.

## Data migration checkpoint

Owner-approved Legacy Demo now has 1,000 original product drafts and 20 categories from the verified consistent SQLite backup, using a separately generated development BusinessOwner and audited normal Tenant activation. Existing imported user confirmations/roles were not upgraded. Originals/hash remain unchanged. Private mapping and original price/discount/stock values remain in ignored `artifacts/legacy-demo-catalog-mapping.json`; Pricing/Inventory/order backfills are pending. Imports are resumable/idempotent. No source product/order/user row was deleted or overwritten. Catalog COPY_ONLY/CHECKSUM/VERIFYONLY/read-only restore/CHECKDB passed before import; repeat backup after substantive backfill/publication checkpoints.

Latest native verification: Catalog 20 tests and gateway 8 tests passed; Catalog API advisory scan reported none. Real browser Catalog/Inventory flow passed 11 checks. Remaining UI/media/production integration limits are tracked in migration progress.

## Approved Legacy Demo batch backfill

See [the verified source migration runbook](../runbooks/legacy-demo-commerce-backfill.md). Catalog 21, Inventory 19 and Pricing 33 affected-service tests passed with real SQL/Identity/Tenant integration. The approved 1,000-product source is published with 1,000 owned price tiers and 76,202 opening-stock units, reconciled per SKU through service APIs. Source prices preserve fixed per-unit discounts and unknown cost; no original database was replaced. The dedicated zero-rate policy is explicitly development-only because source tax metadata is absent. Broker delivery, source order migration and production tax configuration remain separate work.
