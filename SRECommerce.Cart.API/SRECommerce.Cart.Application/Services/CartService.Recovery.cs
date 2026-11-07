using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Application;

public sealed partial class CartService
{
    public async Task<CartRecoveryCandidate?> RecoveryCandidate(Guid tenant, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ct);
        var source = await RecoverySource(ct);
        return source is null ? null : new(source.Id, source.ExpiresAt, source.Items.Count(item => item.Active));
    }

    public async Task<CartView> Recover(Guid tenant, Guid id, RecoverCartRequest request, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ct);
        Id(request.SourceCartId);
        var hash = Hash(new { Action = "recover", request });

        return await repository.Transaction(async token =>
        {
            var target = await Owned(id, false, null, token);
            var replay = await Replay(target, request.OperationId, hash, token);
            if (replay is not null) return replay;

            var source = await RecoverySource(token);
            if (source is null || source.Id != request.SourceCartId)
                throw new BusinessFault("RecoverableCartNotFound", 404);

            await Mutable(target, token);
            repository.Version(target, request.Version);

            var existing = target.Items.Where(item => item.Active).Select(item => item.VariantId).ToHashSet();
            var items = source.Items.Where(item => item.Active && !existing.Contains(item.VariantId)).ToArray();
            if (items.Length == 0) throw new BusinessFault("CartRecoveryItemsAlreadyPresent");

            var products = await access.Products(tenant, items.Select(item => item.VariantId).ToArray(), false, token);
            var restored = 0;
            foreach (var item in items)
            {
                var product = products.SingleOrDefault(value => value.Variants.Any(variant => variant.Id == item.VariantId && variant.Active));
                if (product is null) continue;
                Set(target, product, product.Variants.Single(variant => variant.Id == item.VariantId), item.Quantity);
                restored++;
            }
            if (restored == 0) throw new BusinessFault("CartRecoveryProductsUnavailable");

            source.Status = CartStatus.Merged;
            source.MergedIntoId = target.Id;
            source.Revision++;
            source.UpdatedAt = Now;
            return await Record(target, request.OperationId, hash, "cart.recovered", token);
        }, ct);
    }

    private Task<ShoppingCart?> RecoverySource(CancellationToken ct)
    {
        var userId = access.Actor?.UserId ?? throw new BusinessFault("UserRequired", 401);
        var now = Now;
        return repository.Recoverable(userId, now.AddDays(-30), now, ct);
    }
}
