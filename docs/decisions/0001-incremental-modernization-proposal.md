# ADR 0001: incremental modernization and technology gates

Date: 2026-10-28. Status: Proposed, not adopted or implemented. Approval checkpoint: WAITING_FOR_USER_APPROVAL.

## Context and proposal

The repository has a small reusable monolith, divergent SQLite/SQL Server data, missing Git metadata and little free RAM. Preserve the monolith, recover history, verify a baseline, then extract one approved bounded context at a time. Use the installed SQL Server engine with service-owned databases; Angular stays local initially. Do not pre-create all projects/databases or start all infrastructure.

| Candidate / timing | Business need and interview explanation | Demonstration, test and failure gate | Resource / license notes |
| --- | --- | --- | --- |
| Stable .NET 10 + EF Core SQL Server, first approved service | Supported API runtime and transactional persistence; Clean Architecture separates domain/use cases/adapters | Pin SDK/packages; build and migrate an isolated DB; test auth/tenant/concurrency and restore failure paths | Stable SDK setup pending; legacy project remains separate; SQL Developer non-production only |
| ASP.NET Core Identity and reviewed OAuth2/OIDC choice, Identity | Password/account lifecycle, roles, sessions; separate user storage from token-protocol responsibilities | Legacy BCrypt transition, refresh replay/revocation, lockout, reset/email/MFA and membership tests | Do not add a competing Keycloak identity store; evaluate federation only for a justified later requirement |
| Mailpit, local Identity email demonstration | Capture verification/reset emails without external delivery | Verify generated message/token workflow via local capture; SMTP outage must not falsely mark delivery successful | MIT upstream; select/pin image and confirm tools/health check before setup |
| YARP, separate gateway phase | Route legacy/new services through a stable public entry point | Route/authorization/CORS/rate/timeout tests; downstream outage and internal-route denial | One extra small process; check exact selected package license/version before adoption |
| RabbitMQ, first actual asynchronous workflow | Durable notification/work delivery; acknowledgments and DLQ handle failures | SQL outbox, publisher confirms, retries, inbox deduplication, poison message/DLQ recovery and broker outage; no silent event loss | Upstream server MPL-2.0; image/client license and version checks still required; budget roughly 512 MiB-1 GiB initially and measure |
| Valkey, published theme/layout/catalog read caching | Reduce repeated reads; cache is replaceable and never authoritative for stock/payment | Tenant/version-prefixed keys, TTL, invalidation, stampede protection; cache down/cold start/schema-invalid tests | Upstream BSD-3-Clause; exact image/client checks pending; initial capped test budget ~128-256 MiB and measure |
| OpenTelemetry + structured logging, first service | Explain a request across processes with correlation IDs; redact secrets and tenant data | Assert trace propagation and log redaction; exporter outage must not block requests | Start with console/local export; collector/dashboard opt-in. Verify chosen packages/licenses before addition; Serilog only if justified |
| Http resilience, first real downstream call | Bound dependency latency; prevent repeated calls to a failed service | Timeout/circuit-open/half-open and fallback tests; no unsafe write retries | Prefer supported .NET resilience APIs, avoid duplicate policy stacks |
| xUnit + SQL integration tests, first service | Validate business/security invariants and real provider behavior | Migrations, unique constraints, transaction/concurrency/tenant tests against isolated existing SQL DB; disposable SQL container only if resources allow | Exact xUnit/Testcontainers package versions/licenses checked at implementation; do not run heavy SQL test containers on current free RAM |
| Playwright, first approved Angular module | Verify browser navigation, responsive/error states and complete user journeys | Auth, cross-tenant denial, cold-cache fallback and checkout tests; deterministic fixtures | Run locally on demand; no cloud browser service required; exact package check pending |
| GitHub Actions, after history/remote is recovered | Reproducible build/test and image validation before review | .NET/Angular tests, migration verification and dependency scan; no deployment or secret disclosure | Repo/account and applicable minute/storage limits unverified; do not promise unlimited free CI |

Each candidate needs its own adopted ADR with the exact version, setup/run commands, compatibility/license evidence, working demonstration, tests and measured resource usage when its phase is approved. This roadmap does not claim those demonstrations already exist.

## Explicit deferrals

- Kafka: only if a named business event stream needs durable retention/replay and multiple independent consumers, and RabbitMQ/outbox cannot meet that use case well. Document event key/tenant ordering, partitions, consumer groups, retention, version evolution, replay, idempotency, disk/RAM costs and failure recovery; obtain approval. Do not send the same event to both brokers without a reason.
- MassTransit and MediatR: optional abstractions, not defaults. Check the exact version and current license before adoption; direct application handlers and the RabbitMQ client may suffice.
- CQRS: separate commands/queries where behavior differs; no mandatory mediator/event-sourcing dependency. Keep domain invariants in the domain and request validation at the application boundary. Evaluate FluentValidation only if it improves the actual validation workload.
- Saga: introduce with distributed checkout/reservation/payment compensation, not simple CRUD. Pair with outbox/inbox and idempotency; define recovery for failed compensation.
- Hangfire, SignalR, gRPC: defer until a concrete scheduled job, live status or internal latency/contract need exists. Avoid two systems owning the same job or identity protocol.
- Aspire: optional orchestration/diagnostics evaluation later; do not maintain competing Compose/Aspire stacks now.
- Prometheus/Grafana and search engines: opt-in after measurements justify them and free RAM is available. Initially use SQL Full-Text only after testing catalog projection/index behavior; installed feature presence is not a search implementation.

## Cost and license evidence

Checked official upstream sources on the audit date; released image/package versions are still to be selected and verified individually.

- [Docker Desktop terms](https://docs.docker.com/subscription-billing/desktop-license/): personal/education/non-commercial open source and qualifying small-business use can be free. Larger commercial organizations and government entities have subscription requirements. The owner's organization eligibility was not assumed.
- [SQL Server 2022 editions](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022/): Developer is for development/test. Production hosting/licensing and any Express constraints need a separate assessment.
- [RabbitMQ upstream license](https://raw.githubusercontent.com/rabbitmq/rabbitmq-server/main/LICENSE): core server MPL-2.0, with some Apache-2.0 files. This does not certify every image dependency/client library.
- [Valkey upstream license](https://raw.githubusercontent.com/valkey-io/valkey/unstable/COPYING): BSD-3-Clause. Pin and verify a release, rather than shipping the unstable branch referenced for source evidence.
- [Mailpit upstream license](https://github.com/axllent/mailpit/blob/develop/LICENSE): MIT. Pin/check the chosen release and image separately.

No production zero-cost promise: servers, domains, storage, backup, observability retention, payment transactions and email/SMS/WhatsApp delivery may cost money. Local captures and payment sandboxes remain the development default.
