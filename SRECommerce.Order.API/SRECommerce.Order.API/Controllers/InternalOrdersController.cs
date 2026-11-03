using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Order.Application;
namespace SRECommerce.Order.API;
[ApiController,Authorize,RequestSizeLimit(65536),Route("internal/v1/tenants/{tenant:guid}/orders")]
public sealed class InternalOrdersController(IOrderService service):ControllerBase
{
 [HttpGet("{id:guid}/context")]public async Task<IActionResult> Context(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Context(tenant,id,ct));
 [HttpGet("{id:guid}/notification-context")]public async Task<IActionResult> NotificationContext(Guid tenant,Guid id,[FromQuery]Guid eventId,[FromQuery]int kind,CancellationToken ct)=>Ok(await service.NotificationContext(tenant,id,eventId,kind,ct));
 [HttpGet("verified-purchase")]public async Task<IActionResult> VerifiedPurchase(Guid tenant,[FromQuery]Guid user,[FromQuery]Guid product,CancellationToken ct)=>Ok(await service.VerifiedPurchase(tenant,user,product,ct));
 [HttpGet("fulfillments/{id:guid}")]public async Task<IActionResult> Shipment(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.ShipmentContext(tenant,id,ct));
 [HttpPost("fulfillments/{id:guid}/status")]public async Task<IActionResult> Status(Guid tenant,Guid id,FulfillmentStateRequest request,CancellationToken ct)=>Ok(await service.ChangeFulfillment(tenant,id,request,true,ct));
 [HttpGet("returns/{id:guid}")]public async Task<IActionResult> Return(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.ReturnContext(tenant,id,ct));
}
