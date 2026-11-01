# Order service — implementation in progress

Open `SRECommerce.Order.sln`. It contains API, Application, Domain, Infrastructure and Tests projects with actual source. The root modern solution also includes these five projects. Each service owns its SQL schema; Order calls dependencies through authenticated HTTP instead of querying their databases.

Implemented code includes customer and guest checkout intents, capability digests, immutable server-side financial snapshots, bounded stock allocation, recoverable and compensating checkout phases, cancellation, fulfillment records, authoritative Shipping callbacks, original-price returns, quarantine receipts, refunds, exchange tracking, invoice references, read-only historical imports, audit and outbox rows. No outbox broker delivery or production payment provider is claimed here.

The Angular orders workspace is at `/workspace/{tenantId}/orders`. It includes paged state filters, order details/history, original totals, policy management, invoice references and cancellation requests with stable retry operation IDs. Customer checkout/payment, customer order history/returns/exchanges and the rest of merchant fulfillment/returns UI are still pending.

## Local verification

Run from the repository root with the pinned .NET SDK:

```powershell
./artifacts/dotnet10/dotnet.exe test SRECommerce.Order.API/SRECommerce.Order.Tests -c Release -m:1 -nr:false
```

The nine current cases pass against uniquely named isolated SQL Server test databases and real Identity/Tenant/Catalog/Pricing/Inventory/Cart test servers. Payment and Shipping are clearly marked **test-only contract doubles** in the test project; they are not production implementations or runtime substitutes. Tests mint confidential clients only in the isolated Identity test database. They do not provision production/runtime accounts or apply migrations to main service databases.

The main Order migration is now applied (15 tables), with verified backup and separate read-only restore/CHECKDB. The three original legacy orders were imported and replay-verified as read-only historical records: exact original totals/shipping and provenance, unlinked customer references, explicit missing payment/tax/destination metadata, zero checkout/payment/outbox side effects. The expanded ten-case Order suite passes; real Payment integration is also covered by Payment's separate fourteen-case suite. Real Shipping, broader outage/concurrency recovery coverage, manual recovery controls and complete customer UI remain work in progress. Publishing and deployment are held at the owner's request.
