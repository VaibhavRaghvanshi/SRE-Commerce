# Shipping HTTP contract

Management base: `/api/v1/tenants/{tenant}/shipping`; gateway prefix `/commerce/shipping/tenants/...`. Current membership/module entitlements are revalidated. Platform support requires `X-Support-Reason`. Unknown JSON fields are rejected.

| Method/path | Permission | Purpose |
| --- | --- | --- |
| GET `/lookups` | Staff | Zones/methods and business currency |
| POST `/zones`, PUT `/zones/{id}` | Owner | `{operationId,code,name,countries,postalPrefixes,active,version}` |
| POST `/methods`, PUT `/methods/{id}` | Owner | Zone/name/type/currency, base/per-unit fee, tax, free/min/max total and unit rules, optional pickup warehouse; stable operation/version |
| GET `/shipments?page=1&status=...` | Staff | Bounded 50-row parcel pages |
| GET `/shipments/{id}/history` | Staff | Parcel/recipient, tracking history and sanitized Order update state |
| POST `/shipments/{id}/status` | Manager | `{operationId,version,status,note}` |
| POST `/shipments/callbacks/{id}/retry` | Manager | Requeue an unsent Order update without changing its request |
| POST `/return-pickups` | Manager | `{id,returnId,warehouseId,windowFrom,windowTo}` for an approved return |
| GET `/return-pickups` | Staff | Latest 50 scheduled/collected/cancelled pickup records |
| POST `/return-pickups/{id}/status` | Manager | `{operationId,version,status,note}`; collect or cancel a scheduled pickup |

Kinds: LocalDelivery 0, StorePickup 1, SandboxCourier 2. Shipment states: Created 0, Packed 1, Dispatched 2, Delivered 3, Cancelled 4, DeliveryException 5. Pickup states: Scheduled 0, Collected 1, Cancelled 2. Store pickup requires an active same-tenant warehouse; non-pickup methods cannot select one. Updates use optimistic rowversion. Exact operation retries return stored receipts; conflicting reuse is 409.

Customer base uses the same tenant path. Guest base is `/api/v1/public/tenants/{tenant}/shipping`:

- `POST /quotes/options`: `{id,preparationId,countryCode,postalCode}`. Guest calls require `X-Cart-Key`; customer calls require the current commerce bearer. The server verifies the Cart preparation/customer/role and returns eligible immutable options. It never accepts browser totals/quantities or returns private pickup warehouse IDs. No options means the destination/order is not eligible for currently configured methods.
- `GET /orders/{orderId}/tracking`: customer ownership or guest `X-Order-Key` is checked against Order; a preparation/Order GUID alone is insufficient. Public tracking returns parcel/history without recipient contacts.

Internal base `/internal/v1/tenants/{tenant}/shipping` requires scoped Order machine identity and is blocked at YARP:

- `GET /quotes/{id}` returns the exact amount, currency, destination, quantities, private pickup restriction, preparation ID and customer binding required by checkout.
- `POST /shipments` with `{fulfillmentId}` reads authoritative Order context; a caller cannot supply parcel contents, recipient or a warehouse to override the paid Order.

Shipping uses the dedicated Cart `preparations/{id}/shipping-verify` route and Inventory's bounded warehouse read routes. Its machine identity cannot mutate stock/cart claims. Order callbacks persist an exact `{operationId,version,status,courierReference}` before sending; dispatch/delivery/cancellation map to Order fulfillment statuses 2/3/4. Delivery does not settle real cash or contact a real carrier. The configured payment/courier providers remain local simulations.
