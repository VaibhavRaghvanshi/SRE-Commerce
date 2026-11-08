using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Payment.Application;

namespace SRECommerce.Payment.API;

[ApiController, Authorize, RequestSizeLimit(4096)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/tenants/{tenant:guid}/payments/gift-cards")]
public sealed class GiftCardsController(GiftCardService cards) : ControllerBase
{
    [HttpGet]
    public Task<GiftCardView[]> Issued(Guid tenant, [FromQuery] int page = 1, CancellationToken ct = default)
        => cards.List(tenant, true, page, ct);

    [HttpPost]
    public Task<GiftCardView> Issue(Guid tenant, IssueGiftCardRequest request, CancellationToken ct)
        => cards.Issue(tenant, request, ct);

    [HttpPost("{id:guid}/void")]
    public Task<GiftCardView> Void(Guid tenant, Guid id, CancellationToken ct) => cards.Void(tenant, id, ct);

    [HttpGet("mine")]
    public Task<GiftCardView[]> Mine(Guid tenant, [FromQuery] int page = 1, CancellationToken ct = default)
        => cards.List(tenant, false, page, ct);

    [HttpPost("claim")]
    public Task<GiftCardView> Claim(Guid tenant, ClaimGiftCardRequest request, CancellationToken ct)
        => cards.Claim(tenant, request, ct);
}
