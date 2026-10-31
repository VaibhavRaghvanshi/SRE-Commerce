using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;
using SRECommerce.BuildingBlocks.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace SRECommerce.Identity.API;

[EnableRateLimiting("auth")]
public sealed class AuthorizationController(IdentityDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations, IIdentityAccounts accounts, IAntiforgery antiforgery) : Controller
{
    private static string SafeReturn(string? url) => url is not null && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\') ? url : "/";
    private string Encode(string value) => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(value);

    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null, bool mfa = false, bool error = false)
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext);
        var content = $"<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>SRECommerce sign in</title><h1>SRECommerce sign in</h1><p>{(error ? "Unable to sign in. Verify the account, credentials and authenticator code." : "Use your verified account.")}</p><form method=\"post\" action=\"/account/login\"><input type=\"hidden\" name=\"{token.FormFieldName}\" value=\"{Encode(token.RequestToken!)}\"><input type=\"hidden\" name=\"returnUrl\" value=\"{Encode(SafeReturn(returnUrl))}\"><input type=\"hidden\" name=\"mfa\" value=\"{mfa.ToString().ToLowerInvariant()}\">";
        content += mfa ? "<label>Authenticator or recovery code <input name=\"code\" autocomplete=\"one-time-code\" required></label><label>Use recovery code <input type=\"checkbox\" name=\"recovery\" value=\"true\"></label>" : "<label>Email <input type=\"email\" name=\"email\" autocomplete=\"username\" required></label><label>Password <input type=\"password\" name=\"password\" autocomplete=\"current-password\" required></label>";
        return Content(content + "<button type=\"submit\">Sign in</button></form></html>", "text/html");
    }

    [HttpPost("/account/login")]
    public async Task<IActionResult> LoginPost([FromForm] string? email, [FromForm] string? password, [FromForm] string? code, [FromForm] string? returnUrl, [FromForm] bool mfa, [FromForm] bool recovery, CancellationToken ct)
    {
        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(new { error = "Invalid form token." }); }
        var url = SafeReturn(returnUrl);
        Microsoft.AspNetCore.Identity.SignInResult result;
        ApplicationUser? user;
        if (mfa)
        {
            user = await signIn.GetTwoFactorAuthenticationUserAsync();
            result = recovery ? await signIn.TwoFactorRecoveryCodeSignInAsync(code?.Replace(" ", "") ?? "")
                : await signIn.TwoFactorAuthenticatorSignInAsync(code?.Replace(" ", "") ?? "", false, false);
        }
        else
        {
            user = string.IsNullOrWhiteSpace(email) ? null : await users.FindByEmailAsync(email.Trim());
            var active = user is not null && await db.Profiles.AnyAsync(p => p.UserId == user.Id && p.IsActive, ct);
            result = active && !string.IsNullOrEmpty(password) && password.Length <= 256
                ? await signIn.PasswordSignInAsync(user!, password, false, lockoutOnFailure: true)
                : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        }
        await accounts.AuditAsync(user?.Id, null, "login", result.Succeeded ? "success" : result.RequiresTwoFactor ? "mfa-required" : "failure", ct);
        if (result.RequiresTwoFactor) return Redirect("/account/login?mfa=true&returnUrl=" + Uri.EscapeDataString(url));
        if (!result.Succeeded) return Redirect("/account/login?error=true&returnUrl=" + Uri.EscapeDataString(url));
        return LocalRedirect(url);
    }

    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> Authorize(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded)
            return Challenge(new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + Request.QueryString }, IdentityConstants.ApplicationScheme);
        var user = await users.GetUserAsync(authentication.Principal!);
        if (user is null || !user.EmailConfirmed || !await db.Profiles.AnyAsync(p => p.UserId == user.Id && p.IsActive, ct) || await users.IsLockedOutAsync(user)) return Forbid(IdentityConstants.ApplicationScheme);
        var mfa = authentication.Principal!.HasClaim("amr", "mfa");
        if (user.TwoFactorEnabled && !mfa) return Challenge(new AuthenticationProperties { RedirectUri = Request.Path + Request.QueryString }, IdentityConstants.ApplicationScheme);
        Guid? tenantId = null;
        var selectedTenant = (string?)request.GetParameter("tenant_id");
        if (!string.IsNullOrEmpty(selectedTenant))
        {
            if (!Guid.TryParse(selectedTenant, out var parsed) || !await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == user.Id && m.TenantId == parsed && m.IsActive, ct)) return BadRequest(new { error = "invalid_tenant" });
            tenantId = parsed;
        }
        var application = await applications.FindByClientIdAsync(request.ClientId!, ct) ?? throw new InvalidOperationException("Client not registered.");
        var session = new UserSession { UserId = user.Id, TenantId = tenantId, SecurityStamp = user.SecurityStamp!, MfaAuthenticated = mfa };
        var principal = await BuildPrincipal(user, session, request.GetScopes(), ct);
        var authorization = await authorizations.CreateAsync(principal, user.Id.ToString(), (await applications.GetIdAsync(application, ct))!, AuthorizationTypes.Permanent, principal.GetScopes(), ct);
        session.AuthorizationId = (await authorizations.GetIdAsync(authorization, ct))!;
        principal.SetAuthorizationId(session.AuthorizationId);
        db.Sessions.Add(session);
        await accounts.AuditAsync(user.Id, tenantId, "session.create", "success", ct);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/connect/token")]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        if(request.IsClientCredentialsGrantType())
        {
            var service=ServiceManifest.All.SingleOrDefault(s=>ServiceManifest.ClientId(s.Name)==request.ClientId);
            if(service is null || request.GetScopes().Any(scope=>!ServiceManifest.Scopes(service.Name).Contains(scope)))return Denied();
            var serviceIdentity=new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
            serviceIdentity.SetClaim(Claims.Subject,"svc:"+service.Name);serviceIdentity.SetClaim("service_name",service.Name);
            serviceIdentity.SetClaim("client_id",request.ClientId);serviceIdentity.SetClaim("gty","client_credentials");
            var servicePrincipal=new ClaimsPrincipal(serviceIdentity);servicePrincipal.SetScopes(request.GetScopes());servicePrincipal.SetResources("srecommerce.internal");
            servicePrincipal.SetDestinations(_=>[Destinations.AccessToken]);
            await accounts.AuditAsync(null,null,"service.token.issue",service.Name,ct);
            return SignIn(servicePrincipal,OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()) return BadRequest(new { error = "unsupported_grant_type" });
        var authentication = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = authentication.Principal;
        if (principal is null || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var userId) || !Guid.TryParse(principal.GetClaim("sid"), out var sessionId)) return Denied();
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow, ct);
        var user = await users.FindByIdAsync(userId.ToString());
        if (session is null || user is null || user.SecurityStamp != session.SecurityStamp || !user.EmailConfirmed || await users.IsLockedOutAsync(user)
            || !await db.Profiles.AnyAsync(p => p.UserId == userId && p.IsActive, ct) || (user.TwoFactorEnabled && !session.MfaAuthenticated)) return Denied();
        if (session.TenantId is { } tenantId && !await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, ct)) return Denied();
        var scopes = request.IsRefreshTokenGrantType() && !request.GetScopes().IsDefaultOrEmpty ? request.GetScopes() : principal.GetScopes();
        var refreshed = await BuildPrincipal(user, session, scopes, ct);
        refreshed.SetAuthorizationId(session.AuthorizationId);
        return SignIn(refreshed, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult Denied() => Forbid(new AuthenticationProperties(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The account or session is no longer available."
    }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    [HttpGet("/connect/logout")]
    public IActionResult LogoutConfirmation()
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext);
        return Content($"<!doctype html><html lang=\"en\"><title>Sign out</title><h1>Sign out of SRECommerce</h1><form method=\"post\" action=\"{Encode(Request.Path + Request.QueryString)}\"><input type=\"hidden\" name=\"{token.FormFieldName}\" value=\"{Encode(token.RequestToken!)}\"><button>Sign out</button></form></html>", "text/html");
    }

    [HttpPost("/connect/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(new { error = "Invalid form token." }); }
        var cookie = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (cookie.Succeeded && await users.GetUserAsync(cookie.Principal!) is { } user)
        {
            await accounts.RevokeSessionsAsync(user.Id, null, ct);
            await accounts.AuditAsync(user.Id, null, "logout.browser", "all-user-sessions-revoked", ct);
        }
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> BuildPrincipal(ApplicationUser user, UserSession session, ImmutableArray<string> scopes, CancellationToken ct)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Name, (await db.Profiles.SingleAsync(p => p.UserId == user.Id, ct)).FullName);
        identity.SetClaim(Claims.Email, user.Email);
        identity.SetClaim("sid", session.Id.ToString());
        identity.SetClaims(Claims.Role, (await users.GetRolesAsync(user)).ToImmutableArray());
        if (session.TenantId is { } tenantId)
        {
            var member = await db.Memberships.IgnoreQueryFilters().SingleAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.IsActive, ct);
            identity.SetClaim("tenant_id", tenantId.ToString());
            identity.SetClaim("tenant_role", member.Role);
        }
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        var resources = new List<string>();
        if (principal.HasScope("identity_api")) resources.Add("srecommerce.identity");
        if (principal.HasScope("tenant_api")) resources.Add("srecommerce.tenant");
        if (principal.HasScope("commerce_api")) resources.Add("srecommerce.commerce");
        principal.SetResources(resources);
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Name when principal.HasScope(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when principal.HasScope(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email => [],
            _ => [Destinations.AccessToken]
        });
        return principal;
    }
}
