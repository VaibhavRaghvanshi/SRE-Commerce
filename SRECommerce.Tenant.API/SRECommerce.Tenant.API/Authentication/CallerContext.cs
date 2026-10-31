using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Infrastructure;

namespace SRECommerce.Tenant.API;

public sealed class CallerContext(IHttpContextAccessor contexts) : ITenantCaller
{
    public TenantCaller Current => contexts.HttpContext?.Items[typeof(TenantCaller)] as TenantCaller ?? new(Guid.Empty,"",false,null,null);
}
