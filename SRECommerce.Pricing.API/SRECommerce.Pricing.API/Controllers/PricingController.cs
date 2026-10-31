using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Pricing.Application;
namespace SRECommerce.Pricing.API;
[ApiController,Authorize,RequestSizeLimit(1048576),Route("api/v1/tenants/{tenant:guid}/pricing")]
public sealed class PricingController(IPricingService pricing):ControllerBase
{
    [HttpPost("imports")]public async Task<IActionResult> Import(Guid tenant,PriceImportRequest request,CancellationToken ct)=>Ok(await pricing.Import(tenant,request,ct));
    [HttpGet("lookups")]public async Task<IActionResult> Lookups(Guid tenant,CancellationToken ct)=>Ok(await pricing.Lookups(tenant,ct));
    [HttpPut("settings")]public async Task<IActionResult> Settings(Guid tenant,SettingsRequest request,CancellationToken ct)=>Ok(await pricing.Settings(tenant,request,ct));
    [HttpPost("tax-policies")]public async Task<IActionResult> Tax(Guid tenant,TaxRequest request,CancellationToken ct)=>StatusCode(201,await pricing.Tax(tenant,null,request,ct));
    [HttpPut("tax-policies/{id:guid}")]public async Task<IActionResult> Tax(Guid tenant,Guid id,TaxRequest request,CancellationToken ct)=>Ok(await pricing.Tax(tenant,id,request,ct));
    [HttpPost("groups")]public async Task<IActionResult> Group(Guid tenant,GroupRequest request,CancellationToken ct)=>StatusCode(201,await pricing.Group(tenant,null,request,ct));
    [HttpPut("groups/{id:guid}")]public async Task<IActionResult> Group(Guid tenant,Guid id,GroupRequest request,CancellationToken ct)=>Ok(await pricing.Group(tenant,id,request,ct));
    [HttpPut("group-members/{user:guid}")]public async Task<IActionResult> Member(Guid tenant,Guid user,GroupMemberRequest request,CancellationToken ct)=>Ok(await pricing.AssignGroup(tenant,user,request,ct));
    [HttpGet("group-members/{user:guid}")]public async Task<IActionResult> Member(Guid tenant,Guid user,CancellationToken ct)=>Ok(await pricing.GroupMember(tenant,user,ct));
    [HttpPost("price-lists")]public async Task<IActionResult> List(Guid tenant,PriceListRequest request,CancellationToken ct)=>StatusCode(201,await pricing.List(tenant,null,request,ct));
    [HttpPut("price-lists/{id:guid}")]public async Task<IActionResult> List(Guid tenant,Guid id,PriceListRequest request,CancellationToken ct)=>Ok(await pricing.List(tenant,id,request,ct));
    [HttpGet("prices")]public async Task<IActionResult> Prices(Guid tenant,[FromQuery]int page=1,[FromQuery]Guid? list=null,[FromQuery]string? search=null,CancellationToken ct=default)=>Ok(await pricing.Prices(tenant,page,list,search,ct));
    [HttpPost("prices")]public async Task<IActionResult> Price(Guid tenant,PriceRequest request,CancellationToken ct)=>StatusCode(201,await pricing.Price(tenant,null,request,ct));
    [HttpPut("prices/{id:guid}")]public async Task<IActionResult> Price(Guid tenant,Guid id,PriceRequest request,CancellationToken ct)=>Ok(await pricing.Price(tenant,id,request,ct));
    [HttpGet("prices/{id:guid}/history")]public async Task<IActionResult> History(Guid tenant,Guid id,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await pricing.History(tenant,id,page,ct));
    [HttpPost("promotions")]public async Task<IActionResult> Promotion(Guid tenant,PromotionRequest request,CancellationToken ct)=>StatusCode(201,await pricing.Promotion(tenant,null,request,ct));
    [HttpPut("promotions/{id:guid}")]public async Task<IActionResult> Promotion(Guid tenant,Guid id,PromotionRequest request,CancellationToken ct)=>Ok(await pricing.Promotion(tenant,id,request,ct));
    [HttpPost("coupons")]public async Task<IActionResult> Coupon(Guid tenant,CouponRequest request,CancellationToken ct)=>StatusCode(201,await pricing.Coupon(tenant,null,request,ct));
    [HttpPut("coupons/{id:guid}")]public async Task<IActionResult> Coupon(Guid tenant,Guid id,CouponRequest request,CancellationToken ct)=>Ok(await pricing.Coupon(tenant,id,request,ct));
    [HttpPost("quotes")]public async Task<IActionResult> Quote(Guid tenant,QuoteRequest request,CancellationToken ct)=>Ok(await pricing.Quote(tenant,request,false,ct));
    [HttpGet("quotes/{id:guid}")]public async Task<IActionResult> Quote(Guid tenant,Guid id,CancellationToken ct)=>Ok(await pricing.QuoteById(tenant,id,false,ct));
}
