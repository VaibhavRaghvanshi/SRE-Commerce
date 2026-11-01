# Payment service

Five substantive projects live in `SRECommerce.Payment.sln`: API, Application, Domain, Infrastructure and Tests. Payment owns `SRECommerce_PaymentDB`. This implements a local sandbox payment ledger, not a live payment processor.

Implemented: authoritative Order amount/currency/precision, simulated Card/UPI/NetBanking/COD methods, scoped machine-only initiation/capture/refund, cancellation fences, currency-bounded partial refunds, signed and replay-safe callbacks, immutable attempt/refund/webhook records, owner configuration, staff read views, sandbox reconciliation and an Angular Payments workspace. There are no card/CVV fields, real-money endpoints or provider credentials exposed to Angular.

Verified on 2026-11-01: **14 Payment tests**, **9 Order regression tests**, **11 Gateway tests**, **39 Angular tests**, and the local optimized Angular build. Payment tests call real Identity/Tenant/Order/Cart/Catalog/Inventory/Pricing test APIs and real isolated SQL databases; Shipping is still the explicitly named test double. Main Payment migrations were reviewed/applied, producing nine tables including EF history. Order's initial main schema (15 tables) and Inventory's additive checkout-compensation migration were also applied sequentially, with COPY_ONLY/checksum/VERIFYONLY/isolated restore/CHECKDB verification. Originals were preserved.

```powershell
# Repository root; Windows-authenticated local SQL Server is required.
./artifacts/dotnet10/dotnet.exe test SRECommerce.Payment.API/SRECommerce.Payment.Tests -c Release -m:1 -nr:false
./scripts/Initialize-PaymentDevelopmentSecrets.ps1
./scripts/Verify-ServiceDatabaseBackup.ps1 -Service Payment
```

`Initialize-PaymentDevelopmentSecrets.ps1` preserves existing configuration and initializes only a private sandbox HMAC key in Payment's user-secrets. Runtime SQL logins and confidential service clients remain on their existing hold. Native Order-to-Payment calls require those separately configured machine credentials; the isolated tests provision their own test-only clients. Publishing/deployment remains on hold. Real Shipping integration and full customer checkout UI remain work in progress.

See [design and SQL ownership](../docs/architecture/payment-service-design.md) and [HTTP contract](../docs/api-contracts/payment-service.md).
