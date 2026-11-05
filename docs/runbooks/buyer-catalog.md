# Member and wholesale storefront

The public store continues to expose only current, visible, non-B2B product publications. A signed-in member of the selected tenant uses the buyer catalog. Owner/Manager/B2BBuyer access can include wholesale publications; ordinary Customer/Staff access cannot reveal them. Draft edits are never returned by either shopping audience. Category/brand archive and product archive rules still apply.

## API

All buyer routes require a current Identity membership, the selected tenant, `commerce_api` and enabled Catalog entitlement:

- `GET /api/v1/tenants/{tenant}/catalog/buyer/products`: existing bounded pagination, search/category/brand/collection filters and collection ordering.
- `GET /api/v1/tenants/{tenant}/catalog/buyer/products/{slug}`: one eligible published snapshot.
- `GET /api/v1/tenants/{tenant}/catalog/buyer/published-products?ids=...`: 1–50 distinct nonempty product IDs, with ineligible/missing products omitted.
- `GET /api/v1/tenants/{tenant}/catalog/buyer/media/{id}`: an owned PNG/JPEG referenced by a current publication eligible for this member. Uploading an image alone does not grant read access.

Buyer image responses use `no-store`, `nosniff` and restrictive CSP through the shared API pipeline. They are never added to a public static directory. Public image routes remain unavailable for assets referenced exclusively by wholesale products.

Cart account wishlist/recent reconciliation delegates the incoming user bearer to the buyer product-ID route and retains customer/tenant ownership checks. It stores identifiers, not a copy of private product descriptions or images. New machine identities and SQL runtime logins are not required for this user request path.

## Angular

`/stores/{slug}` has Business buyer sign in. The normal authorization-code/PKCE callback returns to that store and keeps access tokens in memory. Product list/detail, current price preview and stock availability select their existing authenticated API routes only when the session belongs to that tenant. Another tenant's store uses its public routes. Price and stock remain server-authoritative; the UI does not calculate wholesale prices or inventory.

The managed-image directive recognizes only an exact relative managed URL belonging to the displayed tenant. It fetches the corresponding buyer image through the scoped bearer interceptor, accepts PNG/JPEG and creates a temporary memory object URL. Input/session change, logout or component destruction cancels the request and revokes the URL. External and cross-tenant image sources are never fetched through that authenticated client. No bearer is appended to image URLs, browser storage or query strings.

Public search projections and public presentation blocks retain their public audience. This change does not make B2B documents searchable anonymously or create a new wholesale customer account.

## Local verification

Backend cases in Catalog cover private publication snapshots, absence of draft leakage, anonymous denial and private managed images. Cart cases exercise actual B2BBuyer membership against ordinary Customer membership and wishlist reconciliation. Angular includes private image authorization/logout isolation cases.

With local SQL and the development APIs available, the optional native Chrome script creates one uniquely labelled wholesale product/image, signs in through the actual Angular PKCE flow with the existing local demo owner, checks private detail/wishlist/logout and archives only its own product and saved-product references:

```powershell
./scripts/Start-CommerceDevelopment.ps1 -Services Identity,Tenant,Catalog,Cart,ApiGateway
# Start the built Angular SSR server on localhost:4000 with COMMERCE_PROXY_TARGET=http://127.0.0.1:5180.
node scripts/verify-buyer-catalog-browser.mjs --apply
./scripts/Stop-CommerceDevelopment.ps1
```

The browser script uses the existing owner's qualifying membership. Actual B2BBuyer/Customer role separation is covered by isolated SQL/API tests. The browser script is prepared; consult the dated progress checkpoint for whether it was executed successfully. No SQL schema migration, runtime credential provisioning, application publishing or payment operation is part of this feature.
