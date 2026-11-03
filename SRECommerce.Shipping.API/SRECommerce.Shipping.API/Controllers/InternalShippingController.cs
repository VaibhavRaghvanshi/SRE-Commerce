using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.API;
[ApiController,Authorize(Policy="Machine"),RequestSizeLimit(16384),Route("internal/v1/tenants/{tenant:guid}/shipping")]
public sealed class InternalShippingController(IShippingService service):ControllerBase
{
 [HttpGet("quotes/{id:guid}")]public async Task<IActionResult> Quote(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Quote(tenant,id,ct));
 [HttpPost("shipments")]public async Task<IActionResult> Shipment(Guid tenant,ShipmentStartRequest request,CancellationToken ct)=>Ok(await service.CreateShipment(tenant,request.FulfillmentId,ct));
}
