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
| GET | me/security-history?page=1 | Own audit events; 50 records per page |
| POST | me/mfa/setup | Current password required; returns authenticator secret once, preserve only the verified enrollment session |
| POST | me/mfa/enable | Valid TOTP required; returns 8 recovery codes once, revokes sessions and requires re-login |
| POST | me/mfa/disable | MFA-authenticated session plus valid TOTP; revokes sessions |

Account-kind JSON values currently follow the enum numeric values: Customer=0, BusinessOwner=1, Staff=2, SuperAdmin=3, B2BBuyer=4. Self-registration rejects Staff/SuperAdmin. Tenant staff roles are independent of global profile kind; a person may be a customer in one business and staff in another.

## Tenant and privileged APIs

| Method | /api/v1 route | Access |
| --- | --- | --- |
| GET | tenants/{tenantId}/memberships | Current selected tenant + members.read permission |
| PUT | tenants/{tenantId}/memberships/{userId} | members.write; only Staff/Customer/B2BBuyer assignments; cannot alter Owner/Manager |
| DELETE | tenants/{tenantId}/memberships/{userId} | members.write; only non-administrative membership deactivation |
| PUT | platform/tenants/{tenantId}/memberships/{userId} | Current stored SuperAdmin; explicit audited provisioning, including Owner/Manager; serializable transaction protects the last owner, including concurrent demotions |
| PUT | platform/accounts/{userId}/activation | Current stored SuperAdmin; audited activation/deactivation; platform-admin lifecycle deliberately excluded |
| GET | platform/audit?tenantId=...&page=1 | Current stored SuperAdmin, audited privileged read, bounded 50-row pages |

No Tenant service/table is read. Provisioning a GUID reference does not create/activate a business; future Tenant lifecycle verification remains an integration dependency. Tenant headers do not select authorization scope. Membership changes revoke only that user's sessions for the affected tenant; global account/security changes revoke all that user's sessions.

Statuses: 400 validation/protocol errors, 401 invalid authentication, 403 denied scope/permission/account/session, 404 non-owned/missing resource, 409 concurrent/constraint conflict, 429 authentication rate limit, 503 unavailable SQL. Responses and logs must not expose passwords, token values or encrypted email payloads. `/health/live`, `/health/ready` are anonymous process/schema checks. OpenAPI JSON is `/openapi/v1.json` in Development.
