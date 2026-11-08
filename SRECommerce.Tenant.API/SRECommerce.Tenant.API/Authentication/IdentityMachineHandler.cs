using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Tenant.Infrastructure;

namespace SRECommerce.Tenant.API;

public sealed class IdentityMachineHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IdentityConnection identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.Length == 0) return AuthenticateResult.NoResult();
        if (authorization.Length > 16384 || !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(header.Parameter))
            return AuthenticateResult.Fail("Bearer token required.");

        var access = await identity.MachineAsync(header.Parameter, Context.RequestAborted);
        if (access is null || access.Service != "Identity" || access.ClientId != ServiceManifest.ClientId("Identity") ||
            access.Scopes?.Contains("internal_tenant", StringComparer.Ordinal) != true ||
            access.Audiences?.Contains("srecommerce.internal", StringComparer.Ordinal) != true)
            return AuthenticateResult.Fail("Scoped Identity client required.");

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, access.ClientId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
