# Local Docker development — SRE-Commerce

## Verified environment

Windows 11 Home Single Language, 64-bit; Docker Desktop 4.45.0.203075; Docker Client/Server 28.3.3; Compose v2.39.2-desktop.1. Active context `desktop-linux`; server OS `linux`. WSL 3.0.3.0, kernel 6.18.54.1-1; `docker-desktop` distribution running on WSL 2. WSL and VirtualMachinePlatform Windows features enabled; hypervisor running.

Docker Server responded, `docker ps` succeeded, and `docker run --rm hello-world` printed its success message. No WSL update, distribution install/reset, Windows feature change, administrator operation or restart was needed. The Windows Docker service being stopped did not prevent the working WSL engine. Processor feature flags inside an active hypervisor were not treated as proof that BIOS virtualization was disabled.

Laptop has about 7.8 GiB RAM, four logical processors; Docker reported about 3.73 GiB engine memory. Available Windows memory was below 1 GiB during the audit. C: had about 53 GiB and D: about 48 GiB free. Compose limits Identity to 512 MiB/1 CPU and Mailpit to 128 MiB/0.25 CPU. Build memory can exceed runtime limits; keep other heavy applications closed while building. No global WSL resource setting was changed.

## Docker concepts — simple Hinglish

- **Docker:** application aur uski runtime dependencies ko isolated environment mein chalata hai.
- **Image:** application chalane ka packaged template. Dockerfile se Identity image build hoti hai.
- **Container:** image ka running instance. Container stop karne se named volume ka data delete nahi hota.
- **Docker Compose:** `compose.yaml` se related containers, ports, network aur volumes ek saath manage hote hain.
- **Desktop aur WSL 2:** Docker Desktop Windows par management app hai; Linux containers WSL 2 ke Linux kernel par run hote hain. Is machine par existing `docker-desktop` distribution sufficient hai.

## Is repository mein kya run hota hai

Only opt-in `identity-api` profile: Identity API and Mailpit. Mailpit development emails capture karta hai; messages internet par send nahi hote. SQL Server existing Windows instance par hi hai. No SQL Server container, Kafka, RabbitMQ, Redis/Valkey, gateway or additional microservice starts with this profile. Existing `srecommerceapi:dev` image is preserved.

RabbitMQ messages ko producer se consumer tak route/queue karta hai. Redis/Valkey cache aur short-lived shared state rakh sakte hain. Inka configuration/startup current Identity phase ke liye required nahi hai, isliye unke containers create nahi kiye gaye. Mailpit SQL outbox dispatcher se SMTP receive karta hai; yeh broker ka substitute nahi hai.

## Local configuration

Repository root: `D:\Projects\SRECommerce`.

Ignored `.env` supplies `IDENTITY_SQL_CONNECTION`. `.env.example` contains names/instructions only. The local restricted SQL login is mapped only to Identity database DML; verified without table-creation or legacy-database access. Login credentials were provisioned during the earlier approved Identity work, before this Docker troubleshooting phase. Do not print `.env`, `docker compose config` without `--quiet`, or full container environment inspection into reports.

Connection shape (placeholders only):

```text
Server=host.docker.internal,1433;Database=SRECommerce_IdentityDB;User ID=<local-runtime-user>;Password=<local-secret>;Encrypt=true;TrustServerCertificate=true
```

Container ka `localhost` us container ko point karta hai. Windows SQL Server ke liye `host.docker.internal` use hota hai. Compose network par Mailpit hostname `mailpit` hai. Existing SQL TCP listener and existing mixed authentication were used; no firewall/global authentication change was made. Local certificate trust is Development-only; production startup rejects that trust bypass and requires trusted TLS/certificates.

SQL schema/PKCE client were initialized in the earlier Identity phase. Docker startup does not run migrations or create databases. An uninitialized database must go through the separately reviewed setup in the [Identity runbook](runbooks/identity-service.md).

## Commands

Run from repository root:

```powershell
docker desktop start
docker --version
docker version
docker context ls
docker info
docker info --format '{{.OSType}}'
docker ps -a
docker compose version
wsl --status
wsl --version
wsl --list --verbose
Get-Process *docker* -ErrorAction SilentlyContinue
docker run --rm hello-world
docker compose --profile identity-api config --quiet
docker compose --profile identity-api build identity
docker compose --profile identity-api up -d --no-build
docker compose --profile identity-api ps
docker compose --profile identity-api logs --tail 50 identity mailpit
Invoke-RestMethod http://localhost:5081/health/ready
```

Ports sirf loopback par publish hain: API `http://localhost:5081`, Mailpit UI `http://127.0.0.1:8025`, SMTP `127.0.0.1:1025`. Mailpit inbox mein local confirmation/reset messages hote hain; access local rakhein. Health endpoints `/health/live` and `/health/ready`; discovery `/.well-known/openid-configuration`; development OpenAPI `/openapi/v1.json`.

Stop/start without deleting data:

```powershell
docker compose --profile identity-api stop
docker compose --profile identity-api start
docker compose --profile identity-api logs --tail 50 identity
docker stats --no-stream
```

Named volumes preserve Mailpit messages, Identity data-protection keys and development certificates. Native fallback Mailpit used a separate ignored `artifacts/identity-mailpit.db`; it must release ports before the container starts. Do not run both on the same ports. No `down -v`, prune, reset or WSL unregister operation is part of this workflow. Back up keys and certificates separately from SQL; they contain sensitive cryptographic material.

Container smoke test (creates one sample development account; no secret values printed):

```powershell
$env:IDENTITY_SMOKE_URL='http://localhost:5081'
$env:IDENTITY_MAILPIT_URL='http://127.0.0.1:8025'
$env:IDENTITY_SMOKE_REPORT='D:\Projects\SRECommerce\artifacts\identity-container-smoke-result.json'
node scripts\smoke-identity.mjs
```

## Verification record

Docker is ready: Client/Server respond, Linux mode verified, `docker ps` succeeds, hello-world passed. Identity image build completed all 19 BuildKit steps (about 9m32s on this laptop), including package restore and Release publish. No image-pull failure occurred. Build logs initially appeared idle through the history reader, but the final completed log confirmed successful work; no reset/rebuild was needed.

Both `srecommerce-local-identity-1` and `srecommerce-local-mailpit-1` are running **healthy**, with zero restarts. API runs as non-root UID/GID 1654. Readiness/liveness returned HTTP 200; development OpenAPI 3.1.1 exposes 22 paths. A real container smoke test passed **13/13** checks: Mailpit delivery, email confirmation, antiforgery login, PKCE exchange, profile, forbidden platform access, rotating refresh and logout revocation. These operations used the Windows host SQL database through the restricted login, proving read/write connectivity. The smoke created one labeled sample account in Identity only.

Observed usage after smoke: Identity about 95.5 MiB/512 MiB, Mailpit about 9.7 MiB/128 MiB; these are snapshots, not peak guarantees. The readiness healthcheck also passes from inside the API container.

Original SQLite SHA256 still matches the pre-change backup. Legacy SQL counts remain three users and ten products. No migrations/schema changes were made during Docker troubleshooting. One later named-instance SQLCMD metadata probe failed with `Shared Memory Provider: No process is on the other end of the pipe` / `Communication link failure`; the explicit `tcp:localhost,1433` probe succeeded, SQL service remained Running and container readiness passed. Use explicit TCP for this Docker connection; no SQL protocol setting was changed.

No Windows restart, administrator/BIOS action, Docker GUI acceptance or other manual action is currently required. Local Development uses HTTP and local SQL certificate trust; production deployment is separate. Development data-protection keys in the Docker volume are not encrypted at rest; protect the local machine/storage and use the production certificate configuration before deployment. No GitHub push was made. Stop here pending the owner's next approved phase.

Official references: [Docker Desktop WSL backend](https://docs.docker.com/desktop/features/wsl/), [Windows installation requirements](https://docs.docker.com/desktop/setup/install/windows-install/).

## Cost and licensing

Is personal portfolio/learning use ke liye Docker Desktop's personal/education terms apply. Docker lists free use for personal use, education, non-commercial open source and businesses with fewer than 250 employees **and** less than USD 10 million annual revenue. Larger organizational/commercial use and government use may require a paid subscription; check the actual organization's terms before using this setup there. [Official Docker Desktop licensing](https://docs.docker.com/subscription-billing/desktop-license/).

Installed SQL Server 2022 Developer is for development/testing, not production. SQL Server 2022 Express is a separate free edition with a 10 GB relational database limit, up to four cores/one socket and a 1,410 MB buffer-pool limit; moving to it would require a separate compatibility/capacity decision. No edition change is made here. Production hosting, database licensing, domains, backups and outbound delivery may cost money. [Microsoft edition and scale limits](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022/).

## Approved Tenant backend follow-up

Owner ne baad mein Tenant Management backend approve kiya. Uska [runbook](runbooks/tenant-service.md) aur [completion report](architecture/tenant-completion-report.md) ab available hain. Base Identity profile ke saath explicit overlay use hota hai:

```powershell
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api config --quiet
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api build identity tenant
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api up -d --no-build
docker compose -f compose.yaml -f compose.tenant.yaml --profile tenant-api ps
```

Final Identity/Tenant image builds aur 34 live checks pass hue. Tenant API `http://localhost:5082`; SQL Windows host par hi hai, dedicated approved Tenant login sirf uske data ko access karta hai. Identity ka internal hostname `identity:8080` explicitly allowed hai. Teeno containers healthy hain; no Kafka/cache/broker/SQL replacement add hua. Tenant bearer-only API ke paas persistent protected payloads nahi hain, isliye uska framework key provider ephemeral hai; Identity ke existing named key/certificate volumes preserve hain. Original Identity image rollback tag `srecommerce-identity:identity-checkpoint-7eaf9a8` bhi preserve hai.

Session interruption ke baad Docker temporarily unavailable hua; owner ne Desktop khola, phir Client/Server, Linux mode aur running containers dobara verify hue. Koi reset/prune/unregister nahi hua. Final backend suites: Identity 24/24, Tenant 28/28; Tenant backup/restore/CHECKDB bhi pass. Next module aur GitHub push ke liye separate owner checkpoint remains.
