# Notification API

Owns `SRECommerce_NotificationDB`. The API validates templates, stores preferences, queues proven Order events and records delivery attempts. The solution has API, Application, Domain, Infrastructure and Tests projects.

- Merchant: `/workspace/:tenant/notifications`. Configure order, payment, dispatch, delivery and cancellation templates. Disabled or missing templates do not queue messages.
- Customer: `/stores/:slug/notifications`. Email defaults on; SMS and WhatsApp require opt-in.
- Only the scoped Order client can enqueue. The Notification client resolves the recipient from the Order event, never from the enqueue request. The event ID and payload hash make replays safe.
- Enable the worker with `Notifications__DeliveryEnabled=true` in Development to deliver to local Mailpit on port 1025. SMS and WhatsApp adapters are simulated. The worker retries definite transient failures; uncertain sends go to Needs review. Recipient addresses are removed from completed jobs and hidden from the dashboard.
- `Notifications__DispatchEnabled=true` on Order enables its HTTP outbox bridge. The bridge and notification context require the corresponding scoped runtime clients. They are disabled or unprovisioned in the default local launcher.

Low-stock alerts and RabbitMQ transport are still pending. No external messaging is enabled.
