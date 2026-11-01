# Payment HTTP contract

All amounts and currency precision come from the authenticated Order internal context. No API accepts card numbers/CVV or a browser amount for initiation. JSON rejects unknown input properties. Enum statuses: Pending 0, Authorized 1, Captured 2, Cancelled 3, Failed 4, PartiallyRefunded 5, Refunded 6, CashOnDelivery 7. Responses explicitly mark the Sandbox provider.

## Internal — scoped Order machine identity only

Base: `/internal/v1/tenants/{tenantId}/payments`. These paths are not exposed through YARP.

| Method/path | Request | Behavior |
| --- | --- | --- |
| POST base | `{orderId}` | Obtain authoritative Order proof and create/replay its single payment |
| GET `/orders/{orderId}` | None | Current payment and concurrency version |
| POST `/orders/{orderId}/capture` | `{version}` | Capture authorization; COD requires delivered Order; repeated completed capture is safe |
| POST `/orders/{orderId}/abort` | `{}` | Durable cancellation fence, including before initiation; captured balance requires refund |
| POST `/orders/{orderId}/refunds` | `{id,amount,reason}` | Exact replay key; partial/full captured-balance refund with original currency precision |

Initiation/capture require active module access. Recovery reads, aborts and refunds remain scoped to Order after module disablement. Historical orders cannot initiate payments.

## Management — current tenant membership

Base: `/api/v1/tenants/{tenantId}/payments` (gateway prefix `/commerce/payment/tenants/...`). Platform support requires `X-Support-Reason` and is audited.

| Method/path | Permission | Behavior |
| --- | --- | --- |
| GET base `?page=1&status=...` | Staff | Bounded paged transaction list |
| GET `/orders/{orderId}` | Staff | Payment record |
| GET `/mine/orders/{orderId}` | Customer | Only the current user's payment |
| GET `/orders/{orderId}/history` | Staff | Immutable attempts/refunds |
| POST `/orders/{orderId}/reconcile` | Manager | Compare local sandbox receipt, attempt status and refund balances |
| GET/PUT `/configuration` | Owner | Active methods and new-payment simulation outcome; optimistic version required on update |
| POST `/orders/{orderId}/sandbox-decision` | Manager | `{id,version,outcome}`; explicitly simulate Authorized/Declined through the same signature verifier |

Configuration input is `{active,allowCard,allowUPI,allowNetBanking,allowCOD,outcome,version}`. Sandbox outcome is Authorized 0, Pending 1 or Declined 2. Configuration cannot select a live provider. Human callers cannot directly capture/refund through internal paths.

## Public sandbox boundary

`GET /api/v1/public/tenants/{tenantId}/payments/methods` returns enabled method names and sandbox/configured markers; no private configuration or signing key is returned.

`POST /api/v1/public/tenants/{tenantId}/payments/webhooks/sandbox` requires `X-Sandbox-Timestamp` (Unix seconds) and `X-Sandbox-Signature` (64 hexadecimal characters). The HMAC-SHA256 input is the tenant GUID in N format, a dot, the timestamp, a dot, then the exact UTF-8 request body. Maximum body is 16 KiB; timestamp tolerance is five minutes. Body: `{eventId,paymentId,currency,amount,outcome}` with Authorized/Declined outcome only. The private key is `Sandbox:WebhookSigningKey` from server configuration. Missing signing configuration fails closed.

Valid duplicates replay a tenant-scoped body-hash receipt; conflicting reuse is 409. Amount/currency mismatch is 409. Invalid signature/time/tenant binding is 401. A late authorization after cancellation is recorded with `applied:false`, never resurrecting the payment. The manager simulation endpoint retains the human audit identity and exposes no HMAC key.
