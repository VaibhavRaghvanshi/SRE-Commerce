# Discovery API

Owns `SRECommerce_DiscoveryDB` and its SQL Server Full-Text index. The API uses parameterized prefix queries and tenant scoped result filtering. Public search verifies each hit's publication revision and visibility against Catalog before returning it. Business-only items stay out of public results.

- Merchant: `/workspace/:tenant/discovery`. Refresh the index by Catalog page (50 products per page) and moderate reviews.
- Store: `/stores/:slug/search`. Filter by category, brand or public product attribute. Facets summarize the current verified page. Product pages show related items and approved reviews.
- Only an account with a delivered, unreturned item can write a review. Updates go back to Pending. A separate Manager or Owner can approve or reject with an audit reason.

The index refresh is explicit. The merchant index reads published Catalog pages; search and review reads check the resulting product IDs against Catalog's current `published-products` endpoint. Checkout uses the separate variant-based `shopping-products` endpoint. Product publishing does not yet send a broker event to Discovery. Full-Text population is asynchronous. In Development, the verified-purchase API still requires a scoped Discovery client to call Order.

For the existing local Legacy Demo store, run `node scripts/index-legacy-demo-discovery.mjs` for a read-only count after starting Identity, Tenant, Catalog and Discovery. The explicit `--apply` option uses that store's existing development owner to enable Discovery, refresh every published page and check a known product. The script prints only counts and the search result status.
