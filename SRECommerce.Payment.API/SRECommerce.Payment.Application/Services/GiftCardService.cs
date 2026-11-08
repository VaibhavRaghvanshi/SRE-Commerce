using System.Security.Cryptography;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Payment.Domain;

namespace SRECommerce.Payment.Application;

public sealed class GiftCardService(IPaymentRepository repository, IPaymentAccess access, TimeProvider clock)
{
    private static GiftCardView View(GiftCard card, bool merchant) => new(card.Id, card.Currency,
        card.Value, card.Balance, card.IssuedAt, card.ClaimedAt, card.VoidedAt, merchant ? card.Reference : null);

    private static string CodeHash(string code)
    {
        var normalized = code.Trim();
        if (normalized.Length != 32 || !normalized.All(char.IsAsciiHexDigit))
            throw new BusinessFault("InvalidGiftCardCode", 400);
        return Convert.ToHexString(SHA256.HashData(Convert.FromHexString(normalized)));
    }

    private async Task<UserAccess> Actor(Guid tenant, bool merchant, CancellationToken ct)
    {
        await access.Human(tenant, merchant ? ActorPermission.Owner : ActorPermission.Customer, ct);
        var actor = access.Actor;
        if (actor is null || actor.IsPlatformAdmin ||
            (merchant ? actor.TenantRole != "Owner" : actor.TenantRole is not ("Customer" or "B2BBuyer")))
            throw new BusinessFault("GiftCardRoleRequired", 403);
        return actor;
    }

    public async Task<GiftCardView[]> List(Guid tenant, bool merchant, int page, CancellationToken ct)
    {
        var actor = await Actor(tenant, merchant, ct);
        if (page is < 1 or > 10000) throw new BusinessFault("InvalidGiftCardPage", 400);
        var rows = await repository.GiftCards(merchant ? null : actor.UserId, page, ct);
        return rows.Select(card => View(card, merchant)).ToArray();
    }

    public async Task<GiftCardView> Issue(Guid tenant, IssueGiftCardRequest request, CancellationToken ct)
    {
        var actor = await Actor(tenant, true, ct);
        PaymentRules.Id(request.Id);
        PaymentRules.Amount(request.Value);
        if (decimal.Truncate(request.Value) != request.Value) throw new BusinessFault("GiftCardWholeValueRequired", 400);
        var hash = CodeHash(request.Code);
        var reference = PaymentRules.Reason(request.Reference);
        var currency = await access.GiftCardCurrency(tenant, ct);
        return await repository.Transaction(async token =>
        {
            if (await repository.GiftCard(request.Id, token) is { } prior)
            {
                if (prior.IssuedBy != actor.UserId || prior.CodeHash != hash || prior.Value != request.Value || prior.Reference != reference)
                    throw new BusinessFault("GiftCardIssueKeyReused");
                return View(prior, true);
            }
            if (await repository.GiftCardByCode(hash, token) is not null) throw new BusinessFault("GiftCardCodeAlreadyUsed");
            var card = new GiftCard
            {
                TenantId = tenant, Id = request.Id, IssuedBy = actor.UserId, CodeHash = hash,
                Currency = currency, Reference = reference, Value = request.Value, Balance = request.Value,
                IssuedAt = clock.GetUtcNow()
            };
            repository.Add(card);
            await repository.Save("giftcard.issued", card.Id, null, token);
            return View(card, true);
        }, ct);
    }

    public async Task<GiftCardView> Claim(Guid tenant, ClaimGiftCardRequest request, CancellationToken ct)
    {
        var actor = await Actor(tenant, false, ct);
        var hash = CodeHash(request.Code);
        return await repository.Transaction(async token =>
        {
            var card = await repository.GiftCardByCode(hash, token);
            if (card is null || card.VoidedAt is not null || card.ClaimedBy is { } owner && owner != actor.UserId)
                throw new BusinessFault("GiftCardUnavailable", 404);
            if (card.ClaimedBy is null)
            {
                card.ClaimedBy = actor.UserId;
                card.ClaimedAt = clock.GetUtcNow();
                await repository.Save("giftcard.claimed", card.Id, null, token);
            }
            return View(card, false);
        }, ct);
    }

    public async Task<GiftCardView> Void(Guid tenant, Guid id, CancellationToken ct)
    {
        await Actor(tenant, true, ct);
        PaymentRules.Id(id);
        return await repository.Transaction(async token =>
        {
            var card = await repository.GiftCard(id, token) ?? throw new BusinessFault("GiftCardUnavailable", 404);
            if (card.ClaimedBy is not null) throw new BusinessFault("GiftCardAlreadyClaimed");
            if (card.VoidedAt is null)
            {
                card.VoidedAt = clock.GetUtcNow();
                card.Balance = 0;
                await repository.Save("giftcard.voided", card.Id, null, token);
            }
            return View(card, true);
        }, ct);
    }
}
