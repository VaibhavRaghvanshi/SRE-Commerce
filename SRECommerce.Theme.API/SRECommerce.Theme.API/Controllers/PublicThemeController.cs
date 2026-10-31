using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.API;
[ApiController]
public sealed class PublicThemeController(IThemeOperations themes):ControllerBase
{
    [AllowAnonymous,HttpGet("/api/v1/public/tenants/{tenant:guid}/theme")]
    public async Task<IActionResult> Published(Guid tenant,[FromQuery]string? businessType,[FromQuery]string mode="light",[FromQuery]bool mobile=false,CancellationToken ct=default)
    {var value=await themes.Public(tenant,businessType,mode,mobile,ct);var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ContractJson.Write(value)));Response.Headers["ETag"]='"'+Convert.ToHexString(hash)+'"';Response.Headers.CacheControl="public,max-age=60";return Ok(value);}
    [AllowAnonymous,HttpGet("/api/v1/theme-presets")]public async Task<IActionResult> Presets(CancellationToken ct)=>Ok(await themes.Presets(ct));
    [Authorize,HttpPut("/api/v1/platform/theme-presets/{businessType}")]
    public async Task<IActionResult> Preset(string businessType,SavePreset request,CancellationToken ct){await themes.SavePreset(businessType,request,ct);return NoContent();}
}
