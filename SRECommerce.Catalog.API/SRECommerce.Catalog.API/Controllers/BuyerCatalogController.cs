using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Catalog.Application;

namespace SRECommerce.Catalog.API;

[ApiController, Authorize, Route("api/v1/tenants/{tenant:guid}/catalog/buyer")]
public sealed class BuyerCatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> Products(Guid tenant, [FromQuery] CatalogQuery query, CancellationToken ct) => Ok(await catalog.BuyerProducts(tenant, query, ct));
    [HttpGet("products/{slug}")]
    public async Task<IActionResult> Product(Guid tenant, string slug, CancellationToken ct) => Ok(await catalog.BuyerProduct(tenant, slug, ct));
    [HttpGet("published-products")]
    public async Task<IActionResult> ById(Guid tenant, [FromQuery] Guid[] ids, CancellationToken ct) => Ok(await catalog.BuyerProductsById(tenant, ids, ct));
}
