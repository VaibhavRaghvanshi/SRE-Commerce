using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.API;
[ApiController,AllowAnonymous,RequestSizeLimit(16384),Route("api/v1/public/tenants/{tenant:guid}/shipping")]
public sealed class PublicShippingController(IShippingService service):ControllerBase
{
 [HttpGet("regions")]public async Task<IActionResult> Regions(Guid tenant,CancellationToken ct)=>Ok(await service.Regions(tenant,ct));
 [HttpPost("quotes/options")]public async Task<IActionResult> Quotes(Guid tenant,QuoteOptionsRequest request,CancellationToken ct)=>Ok(await service.Options(tenant,request,true,Request.Headers["X-Cart-Key"].ToString(),ct));
 [HttpGet("orders/{order:guid}/tracking")]public async Task<IActionResult> Tracking(Guid tenant,Guid order,CancellationToken ct)=>Ok(await service.CustomerTracking(tenant,order,true,Request.Headers["X-Order-Key"].ToString(),ct));
}
