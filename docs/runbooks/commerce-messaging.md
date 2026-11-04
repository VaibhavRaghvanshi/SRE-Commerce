# Commerce event delivery and low-stock email

## Local flow

RabbitMQ service-to-service messages ki queue rakhta hai. SQL outbox mein business change aur event ek transaction mein save hote hain. Worker broker confirmation milne ke baad hi outbox ko published mark karta hai. Consumer apni SQL inbox aur business projection ko ek transaction mein commit karke message acknowledge karta hai. Repeat delivery se same event ka business change dobara apply nahi hota.

Implemented routes on `srecommerce.events`:

| Producer | Events | Consumer |
| --- | --- | --- |
| Catalog | `catalog.product.published.v1`, `catalog.product.archived.v1` | Discovery public search projection |
| Order | `order.notice.confirmed.v1`, `order.notice.paid.v1`, `order.notice.dispatched.v1`, `order.notice.delivered.v1`, `order.notice.cancelled.v1` | Notification proof lookup and jobs |
| Inventory | `inventory.low-stock.v1` | Notification subscribed managers |

Only supported event types are published; other service outbox records are preserved for later consumers. HTTP Order notification delivery remains available when enabled explicitly and broker delivery is disabled. Both transports use the same Order notice receipt, so switching transports does not duplicate a completed notification.

Each subscriber has durable quorum main, retry and dead queues under `srecommerce.{service}.events`. Retry uses the subscriber's own exchanges and a 30-second TTL, with at most five retries. It does not rebroadcast to unrelated subscribers. Confirmed retry/DLQ publication precedes the original ACK. Quorum retry queues use `at-least-once` dead lettering and `reject-publish`; duplicate transfer is still possible and is handled by the SQL inbox. See the [RabbitMQ quorum documentation](https://www.rabbitmq.com/docs/quorum-queues#dead-lettering).

The provisioning script declares every supported subscriber before publishers start. A mandatory publish with no route stays pending instead of being marked successful. Existing incompatible queues cause a configuration failure; the app never deletes/recreates them. New events cannot resurrect permanently archived search documents, even when delivery order differs from product revision order. Public search also checks the live Catalog before returning a result.

## Configuration and existing hold

`scripts/Initialize-BackendInfrastructure.ps1` is prepared but remains **unexecuted under the runtime account provisioning hold**. It contains private account generation, exact topic permissions and topology creation. Do not run it just to follow this document. No app is published or deployed by these changes.

After the provisioning gate is released, each participating service needs its private `Messaging:Username`/`Password`, host/port/vhost and `Messaging:Enabled=true`. Source-to-target topic permissions prevent one service from publishing another producer's namespace. Development uses local broker networking; non-Development requires TLS. Credentials belong in user-secrets/environment, never source files or CLI output.

Main workers poll bounded batches, lease outbox rows with rowversion and back off through broker outages. Broker errors log exception type without payloads/credentials. The scanner/consumer do not require all fourteen services to be running together. Kafka remains deferred.

Existing local operator commands (after approved provisioning) are:

```powershell
docker compose -f compose.yaml -f compose.infra.yaml --profile backend-infra ps
docker compose -f compose.yaml -f compose.infra.yaml logs --tail 80 rabbitmq
docker compose -f compose.yaml -f compose.infra.yaml stop rabbitmq valkey
```

Stopping keeps named volumes. Do not prune/reset queues or volumes as a troubleshooting step. Dead letters retain failed synthetic/business events for reviewed recovery; this checkpoint does not expose a blind replay button.

## Low-stock alerts

Inventory's optional `LowStock:Enabled=true` scanner calculates available stock across active branches/warehouses and unexpired batches, subtracting reservations. It emits on entering `available <= reorderLevel`, and at most once every 24 hours while the episode continues. Restocking above the threshold resets the episode. State and outbox save atomically; checks use bounded batches and serializable SQL.

Managers open `/workspace/{tenant}/notifications`, save an enabled **Low stock / Email** template and opt in under **Your low-stock alerts**. Placeholders are `{{sku}}`, `{{available}}`, `{{reorderLevel}}`. Per-person/SKU cooldown is 60–10,080 minutes; email preferences also apply. Old events over a day are acknowledged without sending stale alerts.

Notification resolves the manager's current confirmed email and active Owner/Manager membership through a narrowly scoped Identity internal API. It rechecks membership and opt-in at delivery time. Revoked memberships and opted-out managers are suppressed; an Identity outage leaves the message pending. Stock emails do not use a recipient supplied in the broker payload. Additional Identity scope provisioning is still held for the normal runtime client.

Inventory and Notification have additive `inventory-low-stock-alerts.sql` and `notification-stock-alerts.sql` migrations. Local Mailpit remains the only email adapter; SMS and WhatsApp remain mocks. No external customer message or paid provider was activated.

## Isolated broker check

`MessagingBrokerTests` in Discovery.Tests is opt-in using `SRECOMMERCE_TEST_BROKER_PORT`, `SRECOMMERCE_TEST_BROKER_USER` and `SRECOMMERCE_TEST_BROKER_PASSWORD`. Use a separate loopback broker/vhost `srecommerce-tests` with generated private credentials. It exercises the actual SQL outbox worker, Rabbit publisher confirms, delayed retry, duplicate inbox and poison-event DLQ. Default runs report a skip when the isolated broker is not supplied; a skipped test does not prove broker readiness. Synthetic test databases/containers are separate from runtime accounts and project queues.
