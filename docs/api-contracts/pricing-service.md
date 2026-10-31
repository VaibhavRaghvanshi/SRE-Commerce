# Pricing and promotion service

Independent solution: SRECommerce.Pricing.API/SRECommerce.Pricing.API.sln. Five projects contain real domain rules, DTOs/interfaces, application services, a SQL repository, HTTP access checks, controllers and tests. PriceEngine performs decimal-only calculations; no client total, discount, customer group or tax rate is trusted.

Owned SQL database: SRECommerce_PricingDB. InitialPricing creates PricingSettings, TaxPolicies, PriceLists, ProductPrices, CustomerPriceGroups, CustomerPriceGroupMembers, Promotions, Coupons, PricingQuotes, CouponRedemptions, PriceHistory, AuditEntries, IntegrationOutbox, IntegrationInbox and EF history. PreserveUnknownSourceCost makes CostPrice nullable, retaining unknown source values instead of inventing zero cost. Tenant composite keys/FKs, rowversions, amount/window constraints, unique tier/coupon/checkout keys and query/write guards are present. No cross-service SQL read/FK exists.

## Protected management API

Base /api/v1/tenants/{tenant}/pricing; gateway /commerce/pricing/tenants/{tenant}/pricing. Manager/Owner access is checked against current Identity membership and Tenant module state. Currency configuration and customer-group assignment require Owner. Platform support needs a reason and durable audit; delegated Identity membership checks may additionally require the tenant owner context.

| Route | Behavior |
| --- | --- |
| GET lookups | Settings, tax policies, groups, lists, promotions, coupons and rowversions |
| PUT settings | Currency must match Tenant; explicit decimal precision; active lists block policy changes |
| POST/PUT tax-policies and /{id} | Merchant-configured percentage rate and inclusive/exclusive convention |
| POST/PUT groups and /{id} | Active customer-price groups |
| GET/PUT group-members/{user} | Verify current Customer/B2B membership, assign versioned group |
| POST/PUT price-lists and /{id} | Retail/B2B/group audience, currency, priority and UTC validity window |
| GET/POST prices; PUT prices/{id} | Paged/searchable SKU prices and quantity tiers; MRP, selling amount and optional private cost |
| GET prices/{id}/history | Immutable before/after snapshots, latest 50/page |
| POST/PUT promotions and /{id} | Percentage, fixed amount, bundle percentage or buy/get rule; targets, caps, group/B2B eligibility and schedule |
| POST/PUT coupons and /{id} | Code, linked promotion, global/per-customer usage limits and active state |
| POST quotes | Current caller's verified retail/B2B/group quote |
| GET quotes/{id} | Caller-owned snapshot; audited platform support is separately authorized |

SKU registration verifies the published Catalog snapshot over HTTP. Newly added unpublished draft variants cannot acquire an authoritative price. MRP/selling/cost use decimal(18,4); selling amount cannot exceed MRP. Unknown cost remains null and is never exposed by public quotes. Price updates, history, audit and outbox changes commit atomically.

## Quotes and calculations

Anonymous POST /api/v1/public/tenants/{tenant}/pricing/quotes accepts an idempotency ID, variant quantities and optional coupon code. Authenticated POST quotes uses the actual verified caller; the request cannot choose another user/group. Public calls always receive retail eligibility, even if a bearer happens to be supplied. Catalog independently checks current public/customer/B2B SKU visibility. Required SQL/Catalog/Tenant failures return 503; no stale amount approves a purchase.

Eligible price lists are selected by group/B2B/retail precedence, configured priority, applicable quantity tier and deterministic best-price ties. Active list/tax policies are mandatory. Quotes snapshot the chosen price IDs/versions, quantities, currency/precision, tax metadata, selected offer and server totals. They expire within five minutes, shortened by an earlier selected list/offer expiry. Exact retries replay the snapshot; changed payload/context or expired IDs conflict.

Only the best eligible promotion applies. Offers do not stack. Buy/get discounts require actual qualifying quantities; bundle offers only affect bundle SKUs. Amounts and caps are validated server-side; minor-unit allocation preserves exact totals. Configured tax is calculated after discount, using explicit inclusive/exclusive metadata. The response satisfies Total = Net + Tax and Subtotal - Discount = Total. Fixed discounts/caps are defined on the listed price basis before any exclusive tax; the displayed gross saving includes the associated tax reduction.

The local ConfiguredRateTaxCalculator is a replaceable deterministic adapter. It does not infer a statutory rate, exemption or jurisdiction from the business type. Currency precision is an explicit merchant policy. Production tax/provider validation remains part of hardening.

## Checkout claims and coupon capacity

Internal routes are blocked by the gateway and require real scoped client-credentials identities:

- GET /internal/v1/tenants/{tenant}/pricing/quotes/{id}: Cart or Order.
- POST .../redemptions: Order only, with order ID, quote ID and matching customer ID.
- POST .../redemptions/{orderId}/redeem or release: Order only, with rowversion.

A quote preview consumes no coupon capacity. Order claims the quote in a serializable transaction, enforcing expiry, group/currency context, coupon global/per-customer limits and one checkout per quote. CouponRedemptions also records quote consumption when no coupon applies. Holds last fifteen minutes; expired holds cannot be redeemed and stop counting against capacity. Redeem/release retries are terminal and idempotent. Order/payment compensation integration remains pending; no real money is moved here.

Isolated SQL tests provision ephemeral OAuth clients only inside test Identity databases and exercise actual tokens, service scopes, capacity races and wrong-source denial. New live confidential runtime clients/SQL logins remain outside this checkpoint.

## Angular

/workspace/{tenant}/pricing supports currency policy, taxes, price lists/tiers, private cost, price history, groups/customer assignment, promotions and coupons. Customer names come from the authorized Identity membership endpoint; email/password data is not added to that contract. Protected versions and authority remain on the API.

Public product detail requests a server quote for the selected SKU/quantity/coupon, shows tax/discount totals and cancels obsolete requests. Inventory remains an independent live check. Cart/checkout are not claimed by a displayed quote. The legacy chat overlay is hidden on modern commerce routes to keep mobile controls accessible.

Native ports: Pricing 5187, Gateway 5180, Identity 5181, Tenant 5182, Catalog 5185 and Inventory 5186. The explicit Legacy Demo preparation script enables its development pricing module, without inferring source prices/taxes/costs or charging billing. SQL startup never migrates/seeds automatically.

## Verification and remaining work

Initial full modern regression: 174/174 tests across eight test projects passed. After nullable-cost preservation, Pricing passed 32/32. Pricing dependency advisory scan reported no vulnerable packages. Main SQL migrations and backup/VERIFYONLY/isolated read-only restore/CHECKDB passed. Real Chrome Catalog/Inventory/Pricing flow passed 16 checks, including 100 + 18% configured tax = 118, a 10% coupon = 106.20, and quantity two = 212.40.

An initial combined browser run hit a bounded dependency timeout during cold startup; evidence was retained and the warmed run passed. Production timeouts were not relaxed. Large-tenant batching, source price/stock backfill, Cart/Order compensation, broker delivery, production tax/provider deployment and Docker runtime acceptance remain tracked separately.
