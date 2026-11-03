using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.API;
[ApiController,Authorize,RequestSizeLimit(16384),Route("api/v1/tenants/{tenant:guid}/shipping")]
public sealed class CustomerShippingController(IShippingService service):ControllerBase
{
 [HttpPost("quotes/options")]public async Task<IActionResult> Quotes(Guid tenant,QuoteOptionsRequest request,CancellationToken ct)=>Ok(await service.Options(tenant,request,false,Request.Headers["X-Cart-Key"].ToString(),ct));
 [HttpGet("orders/{order:guid}/tracking")]public async Task<IActionResult> Tracking(Guid tenant,Guid order,CancellationToken ct)=>Ok(await service.CustomerTracking(tenant,order,false,null,ct));
}
