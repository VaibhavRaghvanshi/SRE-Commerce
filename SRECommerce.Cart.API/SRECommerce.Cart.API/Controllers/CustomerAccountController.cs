using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Cart.Application;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.API;

[ApiController, Authorize, RequestSizeLimit(16384), Route("api/v1/tenants/{tenant:guid}/cart/account")]
public sealed class CustomerAccountController(CustomerAccountService service) : ControllerBase
{
    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(Guid tenant, CancellationToken ct) => Ok(await service.Addresses(tenant, ct));
    [HttpPut("addresses/{id:guid}")]
    public async Task<IActionResult> SaveAddress(Guid tenant, Guid id, AddressInput input, CancellationToken ct) => Ok(await service.SaveAddress(tenant, id, input, ct));
    [HttpPost("addresses/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid tenant, Guid id, AddressVersion input, CancellationToken ct) { await service.ArchiveAddress(tenant, id, input, ct); return NoContent(); }
    [HttpGet("products/{kind:int}")]
    public async Task<IActionResult> Saved(Guid tenant, SavedProductKind kind, CancellationToken ct, [FromQuery] int page = 1) => Ok(await service.Saved(tenant, kind, page, ct));
    [HttpPut("products/{kind:int}/{product:guid}")]
    public async Task<IActionResult> Save(Guid tenant, SavedProductKind kind, Guid product, SavedProductInput input, CancellationToken ct) => Ok(await service.SaveProduct(tenant, kind, product, input, ct));
    [HttpPost("recently-viewed/clear")]
    public async Task<IActionResult> Clear(Guid tenant, CancellationToken ct) { await service.ClearRecent(tenant, ct); return NoContent(); }
}
