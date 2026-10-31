using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Infrastructure;

namespace SRECommerce.Tenant.API;
public sealed class IdentityContextHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,IdentityConnection identity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization=Request.Headers.Authorization.ToString();
        if(string.IsNullOrEmpty(authorization)) return AuthenticateResult.NoResult();
        if(authorization.Length>16384 || !AuthenticationHeaderValue.TryParse(authorization,out var header) || !header.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(header.Parameter)) return AuthenticateResult.Fail("Bearer token required.");
        var access=await identity.AccessAsync(header.Parameter,Context.RequestAborted);
        if(access is null || access.UserId==Guid.Empty || access.Scopes is null || access.Audiences is null
            || !access.Scopes.Contains("tenant_api",StringComparer.Ordinal) || !access.Scopes.Contains("identity_api",StringComparer.Ordinal)
            || !access.Audiences.Contains("srecommerce.tenant",StringComparer.Ordinal)) return AuthenticateResult.Fail("Current Tenant access required.");
        Context.Items[typeof(TenantCaller)]=new TenantCaller(access.UserId,access.AccountKind,access.IsPlatformAdmin,access.SelectedTenantId,access.TenantRole);
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,access.UserId.ToString())],Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name));
    }
}
