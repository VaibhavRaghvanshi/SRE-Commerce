using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.API;
[ApiController,Authorize,Route("api/v1/tenants/{tenant:guid}/pages")]
public sealed class StorefrontController(IStorefrontOperations pages,ModuleAccess access):ControllerBase
{
    [HttpGet]public async Task<IActionResult> List(Guid tenant,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await pages.List(tenant,page,ct));
    [HttpPost]public async Task<IActionResult> Create(Guid tenant,CreatePage request,CancellationToken ct){var value=await pages.Create(tenant,request,ct);return Created($"/api/v1/tenants/{tenant}/pages/{value.Id}",value);}
    [HttpPut("{id:guid}/draft")]public async Task<IActionResult> Save(Guid tenant,Guid id,SavePage request,CancellationToken ct)=>Ok(await pages.Save(tenant,id,request,ct));
    [HttpPost("{id:guid}/publish")]public async Task<IActionResult> Publish(Guid tenant,Guid id,PublishPage request,CancellationToken ct)=>Ok(await pages.Publish(tenant,id,request,ct));
    [HttpPost("{id:guid}/rollback")]public async Task<IActionResult> Rollback(Guid tenant,Guid id,RollbackPage request,CancellationToken ct)=>Ok(await pages.Rollback(tenant,id,request,ct));
    [HttpGet("{id:guid}/versions")]public async Task<IActionResult> Versions(Guid tenant,Guid id,CancellationToken ct)=>Ok(await pages.Versions(tenant,id,ct));
    [HttpGet("{id:guid}/versions/{version:guid}")]public async Task<IActionResult> Version(Guid tenant,Guid id,Guid version,CancellationToken ct)=>Ok(await pages.Version(tenant,id,version,ct));
    [HttpGet("{id:guid}/publications")]public async Task<IActionResult> History(Guid tenant,Guid id,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await pages.History(tenant,id,page,ct));
    [HttpPost("preview")]public async Task<IActionResult> Preview(Guid tenant,[FromBody]LayoutDocument document,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);LayoutRules.Validate(document);return Ok(document);}
}
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
