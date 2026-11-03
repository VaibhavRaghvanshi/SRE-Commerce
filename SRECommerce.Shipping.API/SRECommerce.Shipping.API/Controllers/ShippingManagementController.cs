using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Shipping.Application;using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.API;
[ApiController,Authorize,RequestSizeLimit(65536),Route("api/v1/tenants/{tenant:guid}/shipping")]
public sealed class ShippingManagementController(IShippingService service):ControllerBase
{
 [HttpGet("lookups")]public async Task<IActionResult> Lookups(Guid tenant,CancellationToken ct)=>Ok(await service.Lookups(tenant,ct));
 [HttpPost("zones")]public async Task<IActionResult> Zone(Guid tenant,ZoneRequest request,CancellationToken ct)=>Ok(await service.Zone(tenant,null,request,ct));
 [HttpPut("zones/{id:guid}")]public async Task<IActionResult> Zone(Guid tenant,Guid id,ZoneRequest request,CancellationToken ct)=>Ok(await service.Zone(tenant,id,request,ct));
 [HttpPost("methods")]public async Task<IActionResult> Method(Guid tenant,MethodRequest request,CancellationToken ct)=>Ok(await service.Method(tenant,null,request,ct));
 [HttpPut("methods/{id:guid}")]public async Task<IActionResult> Method(Guid tenant,Guid id,MethodRequest request,CancellationToken ct)=>Ok(await service.Method(tenant,id,request,ct));
 [HttpGet("shipments")]public async Task<IActionResult> Shipments(Guid tenant,[FromQuery]int page=1,[FromQuery]ShipmentStatus? status=null,CancellationToken ct=default)=>Ok(await service.Shipments(tenant,page,status,ct));
 [HttpGet("shipments/{id:guid}/history")]public async Task<IActionResult> History(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.History(tenant,id,ct));
 [HttpPost("shipments/{id:guid}/status")]public async Task<IActionResult> State(Guid tenant,Guid id,ShipmentStateRequest request,CancellationToken ct)=>Ok(await service.Change(tenant,id,request,ct));
 [HttpPost("shipments/callbacks/{id:guid}/retry")]public async Task<IActionResult> Retry(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Recovery(tenant,id,ct));
 [HttpPost("return-pickups")]public async Task<IActionResult> Pickup(Guid tenant,ReturnPickupRequest request,CancellationToken ct)=>Ok(await service.Pickup(tenant,request,ct));
 [HttpGet("return-pickups")]public async Task<IActionResult> Pickups(Guid tenant,CancellationToken ct)=>Ok(await service.Pickups(tenant,ct));
 [HttpPost("return-pickups/{id:guid}/status")]public async Task<IActionResult> Pickup(Guid tenant,Guid id,ReturnPickupStateRequest request,CancellationToken ct)=>Ok(await service.ChangePickup(tenant,id,request,ct));
}
