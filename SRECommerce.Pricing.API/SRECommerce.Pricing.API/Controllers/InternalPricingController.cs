using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Pricing.Application;
namespace SRECommerce.Pricing.API;
[ApiController,Authorize(Policy="Machine"),Route("internal/v1/tenants/{tenant:guid}/pricing")]
public sealed class InternalPricingController(IPricingService pricing):ControllerBase
{
    [HttpGet("quotes/{id:guid}")]public async Task<IActionResult> Quote(Guid tenant,Guid id,CancellationToken ct)=>Ok(await pricing.QuoteById(tenant,id,true,ct));
    [HttpPost("redemptions")]public async Task<IActionResult> Reserve(Guid tenant,RedemptionRequest request,CancellationToken ct)=>Ok(await pricing.Reserve(tenant,request,ct));
    [HttpPost("redemptions/{id:guid}/redeem")]public async Task<IActionResult> Redeem(Guid tenant,Guid id,RedemptionVersionRequest request,CancellationToken ct)=>Ok(await pricing.Finish(tenant,id,true,request.Version,ct));
    [HttpPost("redemptions/{id:guid}/release")]public async Task<IActionResult> Release(Guid tenant,Guid id,RedemptionVersionRequest request,CancellationToken ct)=>Ok(await pricing.Finish(tenant,id,false,request.Version,ct));
}
