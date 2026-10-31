using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.API;
[ApiController]
public sealed class PublicLayoutController(IStorefrontOperations pages):ControllerBase
{
    [AllowAnonymous,HttpGet("/api/v1/public/tenants/{tenant:guid}/pages/{slug}")]
    public async Task<IActionResult> Published(Guid tenant,string slug,[FromQuery]string? businessType,CancellationToken ct)
    {var value=await pages.Public(tenant,slug,businessType,ct);var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ContractJson.Write(value)));Response.Headers.ETag='"'+Convert.ToHexString(hash)+'"';Response.Headers.CacheControl="public,max-age=60";return Ok(value);}
    [AllowAnonymous,HttpGet("/api/v1/layout-templates")]public async Task<IActionResult> Templates(CancellationToken ct)=>Ok(await pages.Templates(ct));
    [Authorize,HttpPut("/api/v1/platform/layout-templates/{businessType}")]
    public async Task<IActionResult> Template(string businessType,SaveTemplate request,CancellationToken ct){await pages.SaveTemplate(businessType,request,ct);return NoContent();}
}
