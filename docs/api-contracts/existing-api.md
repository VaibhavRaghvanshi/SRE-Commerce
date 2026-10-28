# Existing API contract inventory

Audit: 2026-10-28. Routes derived from source, not exercised against a running API. No endpoints were added or changed. Existing URLs have no URL version prefix; the Swagger document is named v1. JSON serialization follows ASP.NET defaults unless overridden at runtime.

| Method | Route | Access | Behavior |
| --- | --- | --- | --- |
| POST | /api/auth/register | Public | Create Customer with BCrypt password and return JWT/name/email/role/expiry |
| POST | /api/auth/login | Public | Verify password and return same auth payload |
| GET | /api/auth/me | Authenticated | Current user ID/name/email/role; claim mapping requires verification |
| GET | /api/categories | Public | All categories ordered by name |
| GET | /api/categories/{slug} | Public | Category by slug |
| POST | /api/categories | Admin or Employee | Create category, optional generated slug |
| PUT | /api/categories/{id} | Admin or Employee | Replace category fields |
| DELETE | /api/categories/{id} | Admin or Employee | Delete category |
| GET | /api/products | Public | q/categoryId/categorySlug filters; page/pageSize; newest/price-asc/price-desc/name sort |
| GET | /api/products/{slug} | Public | Product by slug |
| POST | /api/products | Admin or Employee | Create product; code-level slug conflict check |
| PUT | /api/products/{id} | Admin or Employee | Replace fields including price, discount and stock |
| DELETE | /api/products/{id} | Admin or Employee | Delete product; current FK can cascade order lines |
| GET | /api/home | Public | Enabled ordered category/banner/product blocks with layout/style metadata |
| GET | /api/admin/home-sections | Admin or Employee | List all sections |
| GET | /api/admin/home-sections/{id:int} | Admin or Employee | Read section |
| POST | /api/admin/home-sections | Admin or Employee | Create section |
| PUT | /api/admin/home-sections/{id:int} | Admin or Employee | Update section |
| DELETE | /api/admin/home-sections/{id:int} | Admin or Employee | Delete section and its placements |
| GET | /api/admin/home-sections/{id:int}/items | Admin or Employee | Placements enriched with catalog/banner title and image |
| PUT | /api/admin/home-sections/{id:int}/items | Admin or Employee | Replace placements matching section entity type |
| GET / POST | /api/admin/banners | Admin or Employee | List / create banners |
| PUT / DELETE | /api/admin/banners/{id:int} | Admin or Employee | Update / delete banner |
| POST | /api/admin/uploads | Admin or Employee | Multipart field File; returns relative public URL |
| GET | /api/admin/users | Admin or Employee | List all users |
| POST | /api/admin/users | Admin or Employee | Create user including Admin; privilege boundary needs correction |
| PUT | /api/admin/users/{id:int}/role | Admin or Employee | Change role; body is a JSON string |
| DELETE | /api/admin/users/{id:int} | Admin or Employee | Delete user, guards against deleting the last Admin only |
| GET | /api/orders/mine | Authenticated | Current user's full order history |
| GET | /api/orders/admin | Admin or Employee | Most recent 100 orders |
| POST | /api/orders | Authenticated | Recalculate product prices, validate/decrement stock and persist order/items |
| GET | /api/admin/reports/summary | Admin or Employee | Revenue/orders/customer/stock summary, top products and recent orders |

There are 34 HTTP actions across 10 controllers (combined-method rows above represent two actions each). Public `/uploads/*` static assets and Swagger are configured outside controllers.

Core contract preservation: retain ProductDto/CategoryDto shapes and paginated product envelope; preserve home block type numbers (1 category, 2 banner, 3 product), HomeEntityType numbers (1 category, 2 product, 3 banner), and order monetary snapshots during compatibility routing. Separate theme/layout versioning in new contracts rather than silently changing StyleJson semantics.

Planned new services should publish `/api/v1/...` contracts with explicit tenant authorization and a reviewed compatibility adapter for legacy routes. Gateway routing is not authorization by itself. No logout/refresh/reset/MFA, order status-update, payment, tenant or theme API currently exists.
