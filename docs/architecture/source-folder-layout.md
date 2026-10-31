# Source folder layout

Owner instruction on 2026-10-31: first match `D:\Projects\Cruds\GrowthStack`. Each implemented API has a top-level `SRECommerce.<Service>.API` folder, own `.sln`, and API/Application/Domain/Infrastructure/Tests projects. Angular is `SRECommerce.UI` at root. Canonical `SRECommerce` spelling and assembly identities are preserved.

Identity, Tenant, Theme, Storefront, Catalog, Inventory, Pricing and Cart contain substantive code. Interfaces/DTOs, entities/enums/value objects/rules, data/services/integrations/initialization/repositories and controllers have named directories and named type files. Splitting preserves namespaces, attributes and executable members. This change does not claim new repository implementations or use cases merely because folders exist.

`SRECommerce.BuildingBlocks` holds existing shared contracts/primitives/transport helpers, not shared business tables. `SRECommerce.slnx` aggregates modern backend projects. Each service `.sln` contains its five projects and resolves shared references normally.

The monolith is preserved under `SRECommerce.Legacy.API`, including own solution, .NET 8 SDK pin, regression tests and private SQLite/media. Root SDK/build properties apply to modern services; legacy has an isolated SDK/property boundary. SQLite hash was checked before/after moving. SQL databases, secret IDs, Docker volumes and business/authentication behavior are preserved.

Project references, Dockerfiles/Compose, Angular workspace/output identifiers and active runbook paths are updated. Old paths in ignored historical artifacts remain evidence. No symlink/duplicate source tree is used. Remaining services receive this structure as substantive implementation starts.

## Verified after relocation (historical folder checkpoint)

- Modern aggregate build: 23 projects, zero warnings/errors. All four independent service `.sln` files list their own five projects successfully. All 36 project references across 28 modern/legacy projects resolve.
- Modern tests: Identity 24, Tenant 28, Theme 18, Storefront 25 passed (95 total across the latest service runs). One initial Tenant test returned 503 while other builds were running; a complete isolated Tenant rerun passed 28/28 without increasing production timeouts. Both original and rerun evidence remain in `artifacts/structure-verification`.
- Preserved legacy application: 33/33 regression tests passed from its new .NET 8 solution.
- Renamed Angular workspace: production browser/server build passed, output `SRECommerce.UI/dist/SRECommerce.UI`; ChromeHeadless tests 12/12 passed. Running the new production server returned HTTP 200 for home/products/admin shell routes and its application script. Temporary smoke server was stopped.
- Existing Identity/Tenant Compose configuration validated quietly with the new Dockerfile paths. New Docker images were not rebuilt during this folder-only checkpoint; existing runtime credentials/volumes are preserved.
- Original SQLite SHA256 unchanged; no source DB/schema reset. Relative documentation links and repository/private-value scan passed. Ignored credentials, data, media and artifacts are excluded from Git.
