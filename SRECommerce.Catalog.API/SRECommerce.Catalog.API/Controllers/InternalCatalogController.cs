using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Catalog.Application;
namespace SRECommerce.Catalog.API;
[ApiController,Authorize(Policy="Machine"),Route("internal/v1/tenants/{tenant:guid}/catalog")]
public sealed class InternalCatalogController(ICatalogService catalog):ControllerBase
{
    [HttpGet("variants/{id:guid}")]public async Task<IActionResult> Variant(Guid tenant,Guid id,CancellationToken ct)=>Ok(await catalog.SellableVariant(tenant,id,ct));
}
