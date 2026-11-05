# Remaining delivery work

This is an implementation checkpoint, not a claim that every master-prompt requirement is complete. Fourteen substantive service source trees, separate five-project solutions, Angular areas and owned SQL schemas exist. The latest source includes customer accounts, reporting, managed media, real isolated RabbitMQ delivery, low-stock alerts, Catalog organization, private buyer shopping, platform operations and product recommendations. The dated progress report records actual test/browser/backup evidence.

## Runtime and acceptance

- **Existing explicit hold:** new runtime SQL logins, confidential OAuth service clients and RabbitMQ/Valkey accounts remain unprovisioned. Do not execute the held access/infrastructure provisioning scripts merely because source is present. Existing approved Identity/Tenant credentials remain separate.
- Complete all-service Docker builds/startup, minimum necessary dependency groups, authenticated service recovery and end-to-end checkout/notification/search acceptance once the runtime dependency gate is resolved. Isolated tests and a real temporary broker were verified; that is not proof of normal multi-container runtime acceptance.
- The latest local API/UI launch was rejected by automatic approval review with only `blocked by policy` supplied. The prepared private-buyer browser verifier and real platform/recommendation browser flows remain pending. Earlier account and Catalog-organization browser runs passed for those earlier changes.
- Finish production provider/storage/TLS/secrets/telemetry configuration and full deployment/rollback verification. The owner requested no application publishing until the implementation is ready. Source pushes are authorized; deployment remains deferred.

## Remaining implementation and review

- Finish smaller merchant controls such as definition edit/archive and a complete end-user Catalog import/export flow; creation, category binding and backend import/export already exist. Review owner/manager administration paths instead of counting the existing staff/customer/buyer assignment screen as every privileged membership operation.
- Audit every master requirement against the final implementation and close remaining edge cases, accessibility, validation, responsive and failure-state issues across all customer/admin routes. A successful build or the existence of a module folder is not feature acceptance.
- Plan and implement advanced master extensions in their appropriate phases: POS integration, gift cards, abandoned-cart recovery, localization and GST-ready invoicing remain incomplete. Loyalty/rewards was specified as optional. Existing barcode fields, configurable currency/tax metadata or feature flags do not implement these systems.
- Validate any production tax/invoicing implementation against applicable current requirements. Current configured quote tax values and historical imports are not certification of legal invoice compliance.

Original SQL databases, original SQLite, imported historical order snapshots and retained backups/test databases must stay preserved. Local SQL is the verified MSSQL22 instance at `tcp:localhost,1433`; the stopped default MSSQLSERVER service must not be mistaken for the application endpoint or started simply to replace it. No replacement SQL container, destructive cleanup, WSL reset or application publishing is part of this checkpoint.
