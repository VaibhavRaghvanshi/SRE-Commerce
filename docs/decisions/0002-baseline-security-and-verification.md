# ADR 0002: stabilize the legacy application before service extraction

Date: 2026-10-28. Status: Adopted for the owner-approved baseline phase.

## Decision and rationale

Preserve the existing four-project monolith and SQLite data while confirming SQL Server as the future service persistence target. Make startup schema/seeding operations explicit, fix the demonstrated access/upload/token defects and establish tests before changing data ownership. Avoid a simultaneous frontend/framework/provider rewrite.

Pin the installed stable .NET SDK 8.0.404 with compatible patch roll-forward and no previews. Patch existing EF/JWT dependencies to 8.0.33 and SQLitePCLRaw.bundle_e_sqlite3 to 3.0.5 following the NuGet advisory scan. Keep API routes/data contracts stable. Do not equate an advisory-clean package graph with a supported, fully patched runtime or production-ready application.

Use xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.11.1, xunit.runner.visualstudio 2.8.2 and Microsoft.AspNetCore.Mvc.Testing 8.0.33 for the legacy regression suite. The local installed package metadata identifies xUnit/runner/SQLitePCLRaw bundle as Apache-2.0 and the two Microsoft testing packages as MIT. Exact release metadata was inspected in the local NuGet package cache. Existing .NET/Angular stack licenses remain subject to their upstream terms; no paid dependency was introduced by this phase.

## Working demonstration and tests

- 33 backend tests exercise actual JWT middleware/authorization, real SQLite behavior, uploads, startup gates and checkout; each test factory checks its temporary DB path before creating schema.
- 12 ChromeHeadless Angular tests exercise the current routed shell/account/dashboard plus user-management guards and bearer origin scope. The production build passes within budget.
- 13 real-process HTTP checks cross the built web proxy, Kestrel and a separately restored SQLite copy. Test credentials/tokens are generated and never logged.
- SQL COPY_ONLY/checksum backup, isolated restore and DBCC CHECKDB passed. SQLite consistent backup/integrity/count checks passed; source hash remained unchanged after smoke writes.

See the completion report and runbook for commands and limitations. These tests do not substitute for future SQL Server migration/concurrency/tenant tests.

## Failure scenarios and trade-offs

- Invalid/missing JWT settings reject startup. Expired/wrong-key/wrong-audience/wrong-issuer tokens fail authentication.
- Employees/customers cannot manage accounts; a demoted administrator's old token no longer grants account administration.
- Invalid/oversized raster uploads are rejected before storage; accepted files are served as images with nosniff. Full decoding/scanning/storage isolation remain separate work.
- Schema/seeding flags are rejected outside Development; missing schema gives readiness 503 without hidden migration.
- SQLite EF 8 decimal aggregation is handled using decimal projections materialized into .NET. This preserves decimal precision for the small baseline but is unsuitable as a long-term large-scale reporting approach.
- Stock concurrency, full session revocation and tenant isolation remain explicit pending features, rather than being inferred from these fixes.

## Resource and cost notes

The machine has about 8 GiB RAM and limited free memory. The baseline uses the installed SQL engine, temporary SQLite files and on-demand ChromeHeadless. No Docker engine startup, broker, cache, Kafka, observability stack or cloud test service was required. Test copies and verified backups remain local; they are excluded from Git.

## Interview explanation

Before splitting a legacy system, establish a recoverable baseline and tests around the boundaries that will move. This phase demonstrates why a database connection-string switch is insufficient, why controller role claims need a current privilege check for sensitive operations, why file extensions alone do not validate uploads, and why a successful build does not prove provider-specific queries work. The trade-off is deliberate temporary compatibility code while service ownership and stronger identity/concurrency rules are designed.
