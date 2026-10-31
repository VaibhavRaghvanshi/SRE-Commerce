# Theme API contract

Service-owned SQL: `SRECommerce_ThemeDB`. Native port 5083. Every business URL is rooted at `/api/v1/tenants/{tenantId}/themes`. Call with a current selected-tenant PKCE bearer and `identity_api tenant_api commerce_api`. Identity account/membership state and Tenant module `themes` are checked live; the module must be explicitly enabled on a supporting plan. Manager/Owner can edit; Staff can inspect. Platform support requires `X-Support-Reason`, an audit and current module availability. Caller-supplied tenant headers grant no access.

| Method/path | Body/result |
| --- | --- |
| GET root, `?page=1` | Up to 50 tenant-owned drafts |
| POST root | `{name,preset}` creates draft with validated preset/default |
| PUT `/{id}/draft` | `{version,document}`; returns new SQL rowversion |
| POST `/preview?mode=light&mobile=false` | Validated ThemeDocument and safe CSS variables; no publication |
| POST `/{id}/publish` | `{version,publicationVersion}`; initial publicationVersion null; subsequent writes require current assignment version |
| GET `/{id}/versions`, `/versions/{versionId}` | Immutable version metadata/document |
| GET `/publications?page=1` | Bounded publish/rollback history |
| POST `/rollback` | `{themeVersionId,publicationVersion}` creates next monotonic assignment/history entry |
| GET `/api/v1/theme-presets` | Development/platform preset catalog |
| PUT `/api/v1/platform/theme-presets/{businessType}` | `{version,document}`; reasoned platform administration only |
| GET `/api/v1/public/tenants/{tenantId}/theme?mode=dark&mobile=true&businessType=spices` | Anonymous presentation snapshot/default; representation ETag and max-age 60 |

Document schema 1 contains exact named light/dark colour tokens, desktop/mobile typography and default mode (`light`, `dark`, `system`). Colors are six-digit hex; allowed fonts are system/inter/arial/georgia; font size 12–24, radius 0–32, spacing compact/normal/spacious, heading scale 1.1–1.8 and line height 1.2–2. Background/foreground pairs require WCAG 4.5 contrast. Unknown JSON properties, arbitrary CSS/URLs, unsupported document schemas and invalid typography fail with 400. Stale SQL rowversions/duplicate names/concurrent publication produce 409. No physical deletion or edits to published history.

Publish/rollback use a serializable SQL transaction: immutable version (publish only), tenant assignment, history, audit and outbox commit together. Snapshot cache updates after commit. Sequence guards prevent older cache writers from replacing newer snapshots. One service owns these tables; no cross-service SQL reads/FKs.

Public fallback order is last valid in-process/optional Valkey snapshot, published SQL (two-second bound), bundled business default, bundled neutral. Public routes skip optional bearer introspection so an Identity outage cannot blank presentation. Theme content is non-authoritative: it never approves prices, stock, checkout or permissions. API-down fallback consumption belongs to the later Storefront/Angular integration. Valkey and RabbitMQ credentials/integration are currently on hold; outbox rows remain pending, not claimed delivered.

Health `/health/live`, `/health/ready`; development OpenAPI `/openapi/v1.json`. 401 means missing/current-invalid user, 403 means role/module denied, 404 means absent tenant-owned resource, 503 means live authorization/SQL dependency unavailable. Errors do not reveal credentials or SQL internals.
