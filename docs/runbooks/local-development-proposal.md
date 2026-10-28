# Local development infrastructure proposal

Date: 2026-10-28. Status: design only; no compose.yaml, .env, Dockerfile or container was created. WAITING_FOR_USER_APPROVAL.

## Starting conditions

- Keep the installed MSSQL22 engine and both existing databases. TCP localhost:1433 reaches MSSQL22; container access has not been tested.
- Keep Angular local. Resolve the legacy launch/proxy mismatch (5027 versus 5297) in the baseline phase.
- Docker CLI/Compose exist, but the Linux engine is unavailable. Do not infer an empty container inventory from this error.
- About 0.8 GiB of 7.8 GiB RAM was free at inspection. Reassess memory and owner-approved workload changes before starting containers; do not stop unrelated services to make room.

## Minimal Compose shape, to materialize only in an approved phase

Start only the service needed for the current module. Mailpit may be the first optional dependency for Identity email capture; add RabbitMQ for the first approved async consumer, then Valkey for a measured caching need. No SQL Server container, Kafka or full dashboard stack initially.

```yaml
name: srecommerce-dev
services:
  rabbitmq:
    image: ${RABBITMQ_IMAGE:?Set a verified pinned management image}
    profiles: [messaging]
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:?Set local user}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:?Set local secret}
    ports: ["127.0.0.1:5672:5672", "127.0.0.1:15672:15672"]
    volumes: [rabbitmq-data:/var/lib/rabbitmq]
    networks: [srecommerce]
    mem_limit: 768m
    cpus: 0.5
    restart: unless-stopped
    healthcheck:
      test: [CMD, rabbitmq-diagnostics, -q, ping]
      interval: 20s
      timeout: 10s
      retries: 6
      start_period: 40s
  valkey:
    image: ${VALKEY_IMAGE:?Set a verified pinned image}
    profiles: [cache]
    command: [valkey-server, --maxmemory, 128mb, --maxmemory-policy, allkeys-lru, --appendonly, "yes"]
    ports: ["127.0.0.1:6379:6379"]
    volumes: [valkey-data:/data]
    networks: [srecommerce]
    mem_limit: 256m
    cpus: 0.25
    restart: unless-stopped
    healthcheck:
      test: [CMD, valkey-cli, ping]
      interval: 15s
      timeout: 3s
      retries: 5
networks:
  srecommerce:
    driver: bridge
volumes:
  rabbitmq-data:
  valkey-data:
```

This illustrative YAML is not runtime-validated. The cache settings are local-only, without cache authentication; it must never be exposed outside this development host/network. Set eviction/TTLs according to the data's cache-only role. If Redis is chosen instead, review that exact distribution/version's license and revise the ADR; Valkey is the proposed alternative.

Future `.env.example` should document image tag/digest variables and leave the local broker password unset, with no real credential. Ignore `.env`, database backups, app data, build outputs, logs and secrets in the recovered repository's root ignore rules. Do not overwrite existing ignore rules blindly. Application secrets go through local secret configuration; container credentials need an approved secret strategy, not source-controlled passwords.

Mailpit should get an independent opt-in profile, pinned image, loopback-only SMTP/UI ports (proposed 1025/8025), restart/resource policy and an image-supported health check verified at setup. Do not invent a health command absent from the selected image.

## Commands after a real Compose file is approved and created

```powershell
docker version
docker compose version
docker compose config --quiet
docker compose --profile messaging up -d rabbitmq
docker compose ps
docker compose logs --tail 100 rabbitmq
docker compose stop rabbitmq
```

`config --quiet` validates without printing expanded credentials. `up` creates/starts only the named dependency; `ps`/`logs` inspect it; `stop` retains containers/volumes. These commands were not executed against this proposal. When an approved API Dockerfile exists, use `docker compose build <approved-service>` and its documented health URL. No volume deletion, `down -v`, pruning or database reset is authorized.

## Container and host networking

On the host, localhost means Windows. Inside an API container, localhost means that container. On a Compose network, `rabbitmq:5672` and `valkey:6379` address those services. A containerized API would normally reach host SQL Server using `host.docker.internal,1433` on Docker Desktop, subject to actual route/firewall testing. Windows integrated auth used in this audit does not automatically work from a Linux container; choose and configure a least-privileged supported authentication mechanism for the approved service. Do not enable SQL authentication, change firewall rules or trust arbitrary certificates silently.

Use encryption with a verified certificate/trust chain for production. Local `sqlcmd -C` is not a production certificate strategy. Verify DNS, TCP, credentials, TLS and the precise service database before applying any reviewed migration.

## Resilience demonstrations required later

- Broker down: commit the business operation and outbox atomically, retain undelivered messages, recover with confirms/retries; verify DLQ and duplicate handling.
- Cache down or cold: fall back to authoritative reads with bounded load. For themes/layouts, use last published valid snapshot, then bundled business preset, then global default. Validate schema/version and tenant ownership before reuse.
- SQL down: fail readiness and return bounded errors; never accept payment or inventory success based on cache state.
- Record measured RAM/CPU/disk and explain the trade-off in each module's runbook. These are acceptance criteria, not completed tests.
