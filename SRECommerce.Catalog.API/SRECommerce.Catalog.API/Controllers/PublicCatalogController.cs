using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Catalog.Application;
namespace SRECommerce.Catalog.API;
[ApiController,AllowAnonymous,Route("api/v1/public/tenants/{tenant:guid}/catalog")]
public sealed class PublicCatalogController(ICatalogService catalog):ControllerBase
{
    [HttpGet("shopping-products")]public async Task<IActionResult> ShoppingProducts(Guid tenant,[FromQuery]Guid[] ids,CancellationToken ct)=>Ok(await catalog.ShoppingProducts(tenant,ids,true,ct));
    [HttpGet("lookups")]public async Task<IActionResult> Lookups(Guid tenant,CancellationToken ct)=>Ok(await catalog.Lookups(tenant,true,ct));
    [HttpGet("products")]public async Task<IActionResult> Products(Guid tenant,[FromQuery]CatalogQuery query,CancellationToken ct)=>Ok(await catalog.Products(tenant,query,true,ct));
    [HttpGet("products/{slug}")]public async Task<IActionResult> Product(Guid tenant,string slug,CancellationToken ct)=>Ok(await catalog.PublicProduct(tenant,slug,ct));
    [HttpGet("variants")]public async Task<IActionResult> Variants(Guid tenant,[FromQuery]Guid[] ids,CancellationToken ct)=>Ok(await catalog.PublicVariantIds(tenant,ids,ct));
}
