using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Inventory.Application;
namespace SRECommerce.Inventory.API;
[ApiController,AllowAnonymous,Route("api/v1/public/tenants/{tenant:guid}/inventory")]
public sealed class PublicInventoryController(IInventoryService service):ControllerBase
{
    [HttpGet("availability")]public async Task<IActionResult> Availability(Guid tenant,[FromQuery]Guid[] variants,CancellationToken ct)=>Ok(await service.Availability(tenant,variants,ct));
}
