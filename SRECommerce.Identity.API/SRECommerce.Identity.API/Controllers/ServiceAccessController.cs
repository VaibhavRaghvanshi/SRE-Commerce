using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.Identity.API;

[ApiController,Authorize(Policy="ServiceAccess")]
public sealed class ServiceAccessController:ControllerBase
{
    [HttpGet("/internal/v1/access")]
    public IActionResult Access()=>Ok(new MachineAccess(User.GetClaim("service_name")!,User.GetClaim("client_id")!,User.GetScopes().ToArray(),User.GetAudiences().ToArray()));
}
