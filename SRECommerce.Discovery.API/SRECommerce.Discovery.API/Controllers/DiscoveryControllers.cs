using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.API;

[ApiController, AllowAnonymous, Route("api/v1/public/tenants/{tenant:guid}/discovery")]
public sealed class PublicDiscoveryController(DiscoveryService service) : ControllerBase
{
    [HttpGet("search")] public async Task<IActionResult> Search(Guid tenant, [FromQuery] SearchQuery query, CancellationToken ct) => Ok(await service.Search(tenant, query, ct));
    [HttpGet("suggestions")] public async Task<IActionResult> Suggestions(Guid tenant, [FromQuery] string q, CancellationToken ct) => Ok((await service.Search(tenant, new(Q: q), ct)).Items.Take(6));
    [HttpGet("products/{id:guid}/related")] public async Task<IActionResult> Related(Guid tenant, Guid id, CancellationToken ct) => Ok(await service.Related(tenant, id, ct));
    [HttpGet("products/{id:guid}/reviews")] public async Task<IActionResult> Reviews(Guid tenant, Guid id, CancellationToken ct, [FromQuery] int page = 1) => Ok(await service.PublicReviews(tenant, id, page, ct));
}

[ApiController, Authorize, RequestSizeLimit(8192), Route("api/v1/tenants/{tenant:guid}/discovery")]
public sealed class DiscoveryController(DiscoveryService service) : ControllerBase
{
    [HttpPost("index/pages/{page:int}")] public async Task<IActionResult> Index(Guid tenant, int page, CancellationToken ct)
    {
        var result = await service.Reindex(tenant, page, ct);
        return Ok(new { result.Page, result.PageSize, result.Total, Indexed = result.Items.Length });
    }
    [HttpGet("products/{id:guid}/my-review")] public async Task<IActionResult> MyReview(Guid tenant, Guid id, CancellationToken ct)
    { var value = await service.MyReview(tenant, id, ct); return value is null ? NoContent() : Ok(value); }
    [HttpPut("products/{id:guid}/my-review")] public async Task<IActionResult> SaveReview(Guid tenant, Guid id, ReviewInput input, CancellationToken ct) => Ok(await service.SaveReview(tenant, id, input, ct));
    [HttpGet("moderation")] public async Task<IActionResult> Moderation(Guid tenant, CancellationToken ct, [FromQuery] ReviewState state = ReviewState.Pending, [FromQuery] int page = 1) => Ok(await service.Moderation(tenant, state, page, ct));
    [HttpPut("moderation/{id:guid}")] public async Task<IActionResult> Moderate(Guid tenant, Guid id, ModerationInput input, CancellationToken ct) => Ok(await service.Moderate(tenant, id, input, ct));
}
