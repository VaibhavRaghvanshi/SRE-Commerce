# Discovery API

Owns `SRECommerce_DiscoveryDB` and its SQL Server Full-Text index. The API uses parameterized prefix queries and tenant scoped result filtering. Public search verifies each hit's publication revision and visibility against Catalog before returning it. Business-only items stay out of public results.

- Merchant: `/workspace/:tenant/discovery`. Refresh the index by Catalog page (50 products per page) and moderate reviews.
- Store: `/stores/:slug/search`. Filter by category, brand or public product attribute. Facets summarize the current verified page. Product pages show related items and approved reviews.
- Only an account with a delivered, unreturned item can write a review. Updates go back to Pending. A separate Manager or Owner can approve or reject with an audit reason.

The index refresh is explicit. Product publishing does not yet send a broker event to Discovery. Full-Text population is asynchronous. In Development, the verified-purchase API still requires a scoped Discovery client to call Order.
