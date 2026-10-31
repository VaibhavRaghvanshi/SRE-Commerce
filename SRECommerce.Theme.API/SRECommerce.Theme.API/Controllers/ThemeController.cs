using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.API;
[ApiController,Authorize]
[Route("api/v1/tenants/{tenant:guid}/themes")]
public sealed class ThemeController(IThemeOperations themes,ModuleAccess access):ControllerBase
{
    [HttpGet]public async Task<IActionResult> List(Guid tenant,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await themes.List(tenant,page,ct));
    [HttpPost]public async Task<IActionResult> Create(Guid tenant,CreateTheme request,CancellationToken ct){var value=await themes.Create(tenant,request,ct);return Created($"/api/v1/tenants/{tenant}/themes/{value.Id}",value);}
    [HttpPut("{id:guid}/draft")]public async Task<IActionResult> Save(Guid tenant,Guid id,SaveTheme request,CancellationToken ct)=>Ok(await themes.Save(tenant,id,request,ct));
    [HttpPost("{id:guid}/publish")]public async Task<IActionResult> Publish(Guid tenant,Guid id,PublishTheme request,CancellationToken ct)=>Ok(await themes.Publish(tenant,id,request,ct));
    [HttpPost("rollback")]public async Task<IActionResult> Rollback(Guid tenant,RollbackTheme request,CancellationToken ct)=>Ok(await themes.Rollback(tenant,request,ct));
    [HttpGet("{id:guid}/versions")]public async Task<IActionResult> Versions(Guid tenant,Guid id,CancellationToken ct)=>Ok(await themes.Versions(tenant,id,ct));
    [HttpGet("versions/{id:guid}")]public async Task<IActionResult> Version(Guid tenant,Guid id,CancellationToken ct)=>Ok(await themes.Version(tenant,id,ct));
    [HttpGet("publications")]public async Task<IActionResult> Publications(Guid tenant,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await themes.PublicationHistory(tenant,page,ct));
    [HttpPost("preview")]public async Task<IActionResult> Preview(Guid tenant,[FromBody]ThemeDocument document,[FromQuery]string mode="light",[FromQuery]bool mobile=false,CancellationToken ct=default)
    {await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);if(mode is not("light"or"dark"))return BadRequest();return Ok(new{document,variables=ThemeRules.Variables(document,mode,mobile)});}
}
