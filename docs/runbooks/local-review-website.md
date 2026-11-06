# Local website review and SQL recovery

## Current state — 6 November 2026

**SQL connectivity and the original memory setting have recovered.** After the owner re-established connectivity, the 1536 MB value was still present. The agent ran the normal-connection recovery script, restored `2147483647` and verified normal mode. Five read-only checks passed: original SQL configuration, fourteen main databases online, all 1000 mapped products present/published, three historical order totals reconciled and unchanged source SQLite hash. Fourteen API/Gateway readiness checks passed after startup. Full checkout acceptance is still pending.

Historical incident: the agent's resource tuning changed max server memory to `1536`, which proved too low. SQL returned error 701 and initial normal/DAC rollback attempts failed. Owned APIs/UI were stopped until the owner re-established connectivity. No database deletion, reset, migration or security-setting change was performed during the incident.

The normal recovery path has executed successfully. Its optional privileged restart path is syntax checked only and was not executed by the agent. **No administrator action is currently pending.** If normal connections are unavailable in a future recurrence, the reviewed administrator recovery command is:

```powershell
& 'D:\Projects\SRECommerce\scripts\Restore-LocalSqlMemory.ps1' -RestartRecovery
```

It checks the exact MSSQL22 service binary, requires the existing stopped SQL Agent, stops only owned development API processes, briefly starts MSSQL22 with temporary minimal-configuration/SQLCMD-only arguments, restores the recorded original memory value, and returns the service to normal startup. It verifies the instance name, normal mode and original value before recording success. It does not change credentials, permanent startup parameters, databases or the separate default MSSQLSERVER service. Its local recovery journal is ignored at `artifacts/review-website/sql-memory-setting.json`.

Microsoft documents minimal-configuration startup for recovery after an excessively low memory setting: [server memory configuration](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options?view=sql-server-ver16). The unsuccessful diagnostic attempts followed the [documented DAC mechanism](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/diagnostic-connection-for-database-administrators?view=sql-server-ver16).

Do not reduce this instance's memory limit again without measuring its complete workload, including retained test/restore databases and unrelated databases. Do not delete or offline those databases to free memory without the owner's approval.

## Start and stop after recovery

From the repository root:

```powershell
./scripts/Start-ReviewWebsite.ps1
# Stop only this review UI; API processes remain available:
./scripts/Stop-ReviewWebsite.ps1
# Stop the review UI and owned native development APIs:
./scripts/Stop-ReviewWebsite.ps1 -Apis
```

The intended URL is **http://localhost:4000**. The optimized Angular server binds only to `127.0.0.1`. Native APIs use 5181–5192 and 5194, with YARP at 5180. Manufacturing and Kafka are excluded from this local review group. Starting the helper does not initialize clients, migrate databases, start SQL containers or publish the application. An already-owned UI is reused while API startup is checked; an unrelated process on port 4000 is preserved. SQL must answer the read-only preflight. The helper now runs `verify-review-readiness.mjs` serially and requires all fourteen SQL-backed readiness endpoints to pass. The ignored report records the actual sample time and individual results. Readiness alone does not prove checkout acceptance.

The native launcher opts its child processes into `LocalReview__DependencyTimeouts=true`. This is honored only in **Development**: an outgoing dependency has an 8-second attempt budget and 12-second overall budget, beneath the UI/proxy's existing 15-second boundary. Production and non-opted-in environments retain their existing 2/5-second budgets. Safe reads retain one bounded retry; POST/PUT/PATCH/DELETE/CONNECT are not automatically replayed, including after a timeout. Ten focused tests passed for environment isolation and retry behavior. This uses the [documented .NET resilience configuration](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience). No machine-wide environment or SQL resource setting is changed by this profile.

The homepage uses the actual Legacy Demo Tenant/Catalog. Open **Explore the store** for products, or **For businesses** for workspace selection. Owner login details remain only in `%APPDATA%\Microsoft\UserSecrets\SRECommerce-LegacyDemo-Development\secrets.json`; they must not be pasted into Git, reports or chat. Authentication uses normal code + PKCE and keeps OAuth tokens in memory.

## Approved local access and demo settings

The owner released the previous OAuth hold only for **Identity, Catalog, Inventory, Pricing, Cart, Order, Payment, Shipping and Notification**. Those nine service clients were provisioned and their real client-credentials grants, machine access and rejection of the human `identity_api` scope passed. Their values remain in per-service UserSecrets and ignored local manifests. Existing passwords were preserved. New SQL runtime logins, broker/cache accounts and other confidential service clients remain held. Native Windows-auth SQL does not prove Docker runtime-login acceptance.

The dedicated demo tenant now has sandbox COD/UPI and a labelled India delivery option charging INR 99, with zero configured delivery tax. The first real order-create request exposed a missing Order policy (`OrderingNotConfigured`). After a verified Order backup/read-only restore, its Owner API created a demo-only guest/COD policy, 14-day return window, exchanges enabled and INR 1000000 maximum. Existing policies are preserved by the preparation script. This is a development policy, not a tax-compliance or real-provider claim. Originals were preserved, and pre-change Tenant/Payment/Shipping backups were verified through separate read-only restores. Only seven precisely matched old browser fixture products were soft-archived; the 1,000 mapped original products were explicitly protected.

## Verified evidence and pending acceptance

- Aggregate 75-project backend build: zero warnings/errors. Catalog: 25 isolated SQL/API cases passed, including rejection of archiving a category-bound field.
- Angular: 66 Chrome cases passed. Optimized build evidence is under ignored `artifacts/angular-night-*-build.log`.
- Real browser design checks: 7 passed before the SQL incident, covering homepage/store/workspace desktop/mobile rendering, real owner PKCE and reachable field/import controls. The legacy CSS leak found in screenshots was corrected and the design checks rerun. Later merchant search/header refinements still need a native browser rerun.
- Before the incident all 14 selected API/Gateway readiness endpoints passed, and the review UI's loopback binding was verified. **These are historical checks, not the current runtime status.**
- Full guest checkout verification did **not** pass: initial requests suffered dependency timeouts, then SQL became unavailable. The verifier recorded zero accepted checks and no checkout/order ID. Do not announce a successful order or stock compensation. Its labelled-order cancellation/reconciliation path remains unverified.
- The private buyer image/wishlist/logout verifier and new Catalog CSV write/export browser acceptance remain pending. Existing isolated tests do not replace this runtime evidence.

The read-only `node scripts/verify-local-review-data.mjs` audit passed all five checks. Source SQLite SHA256 remains `FC6D183D231508E14F9505523DE86B94B6891AF18E55692D144F27932AAEFE01`. The native group is running again. Run one browser verifier at a time:

```powershell
node scripts/verify-review-design.mjs
node scripts/verify-checkout-browser.mjs --apply
node scripts/verify-buyer-catalog-browser.mjs --apply
```

The two `--apply` checks write only labelled development fixtures. Checkout must cancel its own test order through the audited business workflow and reconcile original stock; buyer verification must archive only its own product and remove its own saved references. Inspect their ignored `result.json` reports and any recovery requirement. Preserve data/backups; never substitute direct SQL stock edits or row deletion for compensation.

Runtime logs: `artifacts/commerce-development/{Service}.out.log`, matching `.err.log`, and `artifacts/review-website/ui.out.log` / `ui.err.log`. These are private and must not be committed. This is a local review workflow; publishing/deployment remains deferred.
