# Identity API v1

The approved service owns SRECommerce_IdentityDB. Default development origin: `http://localhost:5081`; only local development permits HTTP. Production requires HTTPS, validated SQL certificates and configured signing/encryption certificates. Authentication uses OpenIddict authorization code + mandatory PKCE, not the legacy `/api/auth/login` contract.

## Protocol

| Method | Endpoint | Behavior |
| --- | --- | --- |
| GET | /.well-known/openid-configuration | Discovery, issuer and supported endpoints |
| GET | /connect/authorize | Registered first-party client, exact redirect URI, S256 PKCE, state/nonce; optional tenant_id must match an active membership |
| GET/POST | /account/login | Antiforgery-protected Identity cookie sign-in; verified email, lockout and authenticator/recovery-code step |
| POST | /connect/token | authorization_code or refresh_token only; code verifier required; stored refresh rotation/replay detection |
| POST | /connect/revoke | Standard OpenIddict token revocation for registered client |
| GET/POST | /connect/logout | Antiforgery-protected browser logout; current user's authorization sessions revoked |

Local client: `srecommerce-local`, public client, callback `http://127.0.0.1:5099/callback`, no client secret. Supported scopes: openid, profile, email, offline_access, identity_api. The first-party client is explicitly registered by the initialization command. No arbitrary client registration or wildcard redirects. Production registration must provide an explicit HTTPS redirect. Access tokens last 5 minutes; sessions/refresh grants are capped at 7 days. Protected APIs require identity_api and audience srecommerce.identity, current active account/session/security stamp and valid token/authorization entries.

## Account and self-service APIs

| Method | /api/v1 route | Access / result |
| --- | --- | --- |
| POST | accounts/register | Public; email/password/fullName/kind (Customer, BusinessOwner or B2BBuyer); 202 neutral duplicate response, no tokens |
| POST | accounts/confirmation | Public, email; neutral 202, durable local confirmation email if eligible |
| POST | accounts/confirm-email | Public, email/code; 204 or 400 |
| POST | accounts/forgot-password | Public, email; neutral 202 |
| POST | accounts/reset-password | Public, email/code/password; 204, all sessions revoked; invalid/replayed reset 400 |
| GET/PUT | me | Active session; read/update own profile; cannot change roles or verification flags |
| POST | me/password | CurrentPassword/NewPassword; native Identity policy; revoke old sessions/cookie stamps |
| GET | me/sessions | Only own sessions, identifiers/dates/state; no raw tokens |
| DELETE | me/sessions/{id} | Only own session; 404 for others; revokes authorization/access/refresh |
| POST | logout | Revoke current bearer session and clear browser cookie |
| GET | me/memberships | Own tenant references/roles only |
| GET | me/access | Current server-validated account kind, stored platform role, selected active membership, scopes and audiences for trusted service authorization; no credentials/tokens |
| GET | me/security-history?page=1 | Own audit events; 50 records per page |
| POST | me/mfa/setup | Current password required; returns authenticator secret once, preserve only the verified enrollment session |
| POST | me/mfa/enable | Valid TOTP required; returns 8 recovery codes once, revokes sessions and requires re-login |
| POST | me/mfa/disable | MFA-authenticated session plus valid TOTP; revokes sessions |

Account-kind JSON values currently follow the enum numeric values: Customer=0, BusinessOwner=1, Staff=2, SuperAdmin=3, B2BBuyer=4. Self-registration rejects Staff/SuperAdmin. Tenant staff roles are independent of global profile kind; a person may be a customer in one business and staff in another.

## Tenant and privileged APIs

| Method | /api/v1 route | Access |
| --- | --- | --- |
| GET | tenants/{tenantId}/memberships | Current selected tenant + members.read permission |
| GET | tenants/{tenantId}/memberships/directory | Selected tenant and `members.read`. Supports `search`, `role`, `active` and one-based `page`. Returns up to 25 rows, the filtered total, the tenant's active seat count and a version for each row. |
| PUT | tenants/{tenantId}/memberships/{userId} | `members.write`. Owner can assign Manager, Staff, Customer or B2BBuyer. Manager can assign Staff, Customer or B2BBuyer. Owner changes require platform provisioning. |
| DELETE | tenants/{tenantId}/memberships/{userId} | `members.write`. Owner can deactivate Manager, Staff, Customer or B2BBuyer. Manager can deactivate Staff, Customer or B2BBuyer. |
| PUT | platform/tenants/{tenantId}/memberships/{userId} | Current stored SuperAdmin; explicit audited provisioning, including Owner/Manager; serializable transaction protects the last owner, including concurrent demotions |
| PUT | platform/accounts/{userId}/activation | Current stored SuperAdmin; audited activation/deactivation; platform-admin lifecycle deliberately excluded |
| GET | platform/audit?tenantId=...&page=1 | Current stored SuperAdmin, audited privileged read, bounded 50-row pages |

Identity checks the effective Tenant plan before any new or reactivated membership, including customer self-enrollment and platform provisioning. All active roles consume one seat. A plan downgrade preserves existing memberships and blocks new grants until capacity is available. An exhausted plan returns 409 `MembershipLimitReached`, an ineligible plan returns 403 and an unavailable plan lookup returns 503. Replaying an existing active membership uses no additional seat. Identity owns the membership count and serializes writes per tenant in SQL Server.

The directory's version can be supplied in the merchant PUT body or the DELETE `version` query parameter. A stale change returns 409. Existing callers may omit the version. Tenant headers do not select authorization scope. Membership changes revoke only that user's sessions for the affected tenant. Global account and security changes revoke all that user's sessions.

Tenant integration additionally registers `tenant_api` with resource/audience `srecommerce.tenant`. Clients using Tenant request both `identity_api` and `tenant_api`; Identity-only scopes remain valid for existing Identity APIs. Explicit `--initialize` adds this permission to the known client without replacing redirects/settings. The Tenant runtime obtains current authorization through `/me/access` rather than reading Identity tables. Owner provisioning requires a currently active, email-confirmed account; disabling the owner during onboarding prevents activation. Unchanged active membership provisioning is an audited idempotent operation and preserves sessions; actual role/activation changes still revoke them.

Statuses include 400 for validation or protocol errors, 401 for invalid authentication, 403 for denied access, 404 for a missing resource, 409 for a conflict or exhausted seat allocation, 429 for authentication rate limiting and 503 for unavailable SQL or plan data. Responses and logs omit passwords, tokens and encrypted email payloads. `/health/live` and `/health/ready` are anonymous process and schema checks. OpenAPI JSON is `/openapi/v1.json` in Development.
