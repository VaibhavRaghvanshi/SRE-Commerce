using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Pricing.Application;
namespace SRECommerce.Pricing.API;
[ApiController,AllowAnonymous,RequestSizeLimit(1048576),Route("api/v1/public/tenants/{tenant:guid}/pricing")]
public sealed class PublicPricingController(IPricingService pricing):ControllerBase
{
    [HttpPost("quotes")]public async Task<IActionResult> Quote(Guid tenant,QuoteRequest request,CancellationToken ct)=>Ok(await pricing.Quote(tenant,request,true,ct));
}
