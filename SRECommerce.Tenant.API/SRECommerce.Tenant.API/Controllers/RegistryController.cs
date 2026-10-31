using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Tenant.Application;

namespace SRECommerce.Tenant.API;
[ApiController]
public sealed class RegistryController(ITenantRegistry registry,ITenantOperations operations):ControllerBase
{
    [AllowAnonymous,HttpGet("/api/v1/public/tenants/by-id/{id:guid}")]
    public async Task<IActionResult> ById(Guid id,CancellationToken ct)=>await registry.PublicById(id,ct) is {} value?Ok(value):NotFound();
    [AllowAnonymous,HttpGet("/api/v1/public/tenants/{slug}")]
    public async Task<IActionResult> BySlug(string slug,CancellationToken ct)=>await registry.PublicBySlug(slug,ct) is {} value?Ok(value):NotFound();
    [Authorize,HttpGet("/api/v1/access/tenants/{id:guid}/entitlements")]
    public async Task<IActionResult> Access(Guid id,CancellationToken ct)
    {var value=await operations.MemberEntitlements(id,ct);return Ok(new{value.TenantId,value.Active,Modules=value.Modules.Select(m=>new{m.Module,m.Enabled})});}
}
