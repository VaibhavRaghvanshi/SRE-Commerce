# Shipping service

Open `SRECommerce.Shipping.sln`: API, Application, Domain, Infrastructure and Tests contain substantive source. Shipping owns `SRECommerce_ShippingDB` (13 tables including EF history). Its initial schema and the Payment zero-charge expansion were applied sequentially with owning-database guards, COPY_ONLY/checksum backups, VERIFYONLY, separate read-only restores and CHECKDB on 2026-11-02. Existing data was preserved.

Implemented: tenant-scoped zones/postal coverage, local delivery/store pickup/sandbox courier methods, configurable charges/tax/free thresholds and eligibility, immutable cart-bound shipping quotes, verified Order fulfillment creation, partial parcel quantities, tracking/state history, durable ordered callbacks into Order, retry/manual-review controls, customer/guest tracking and approved-return pickup coordination. Pickup collection does not receive stock or issue refunds; warehouse inspection remains an Order/Inventory workflow.

**14 real SQL/API integration cases pass**, including real Pricing → Cart → Shipping → Order → Payment → Inventory checkout, guest/customer access, exact quote replay, server-authoritative fees, private pickup routing, read-only service scopes, preparation binding, partial delivery, lost callback response, dependency outage, COD settlement, return-pickup rescheduling and zero-total/free-shipping checkout. These integration paths use the actual implemented services and scoped test-only Identity clients; no Payment/Shipping contract doubles are used on this path. Separate older Order/Payment suites retain explicitly named doubles for their narrower isolation.

The Angular Shipping workspace implements zones/methods, shipment history/movements, order-update retries and return pickups. The local Angular build and 42-test frontend suite pass. Full customer checkout/account UI and remaining Notification/Manufacturing/Discovery modules are still active work. Publishing/deployment remains on hold. No runtime SQL account or confidential OAuth client was provisioned in this checkpoint; native cross-service calls need their separately configured credentials.

```powershell
./artifacts/dotnet10/dotnet.exe test SRECommerce.Shipping.API/SRECommerce.Shipping.Tests -c Release -m:1 -nr:false
./scripts/Verify-ServiceDatabaseBackup.ps1 -Service Shipping
# Explicitly selected only; startup performs no migrations/provisioning.
./scripts/Start-CommerceDevelopment.ps1 -Services Identity,Tenant,Catalog,Inventory,Pricing,Cart,Order,Payment,Shipping,ApiGateway
```

See [design/SQL ownership](../docs/architecture/shipping-service-design.md) and [HTTP contract](../docs/api-contracts/shipping-service.md). Courier handling is a local simulation; no external carrier or paid integration is configured.
