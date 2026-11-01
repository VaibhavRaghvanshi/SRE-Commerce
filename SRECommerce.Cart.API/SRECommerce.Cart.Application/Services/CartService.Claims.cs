using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Application;
public sealed partial class CartService
{
    public async Task<object> CancelClaim(Guid tenant,CartClaimRequest request,CancellationToken ct)
    {
        access.Recovery(tenant);Id(request.OrderId);Id(request.PreparationId);
        return await repository.Transaction(async token=>
        {
            var claim=await repository.Claim(request.OrderId,token);var preparation=await repository.Preparation(request.PreparationId,token);
            if(claim is not null&&(claim.PreparationId!=request.PreparationId||claim.UserId!=request.UserId))throw new BusinessFault("CartClaimKeyReused");
            if(claim is not null&&claim.Status is CartClaimStatus.Released or CartClaimStatus.Expired or CartClaimStatus.Completed)return new{request.OrderId,Closed=true};
            if(claim is null)
            {
                // A cancellation fence blocks a delayed claim for this order only. It cannot mutate an unproved cart.
                if(preparation is null||preparation.UserId!=request.UserId||preparation.CustomerRole!=request.TenantRole)return new{request.OrderId,Closed=true};
                claim=new CartCheckoutClaim{TenantId=tenant,Id=request.OrderId,PreparationId=preparation.Id,CartId=preparation.CartId,CartRevision=preparation.CartRevision,UserId=preparation.UserId,Status=CartClaimStatus.Released,CreatedAt=Now,ExpiresAt=Now};repository.Add(claim);
            }
            else if(claim.Status==CartClaimStatus.Held)
            {
                claim.Status=CartClaimStatus.Released;var cart=await repository.Cart(claim.CartId,token)??throw new BusinessFault("CartNotFound",404);
                if(cart.Status==CartStatus.Active){cart.Revision++;cart.UpdatedAt=Now;}
            }
            // A completed cart stays closed; a cancelled order never reopens another active customer cart.
            await repository.Commit("cart.checkout.cancelled",request.OrderId,new{OrderId=request.OrderId,claim.CartId},token);return new{request.OrderId,Closed=true};
        },ct);
    }
    private async Task Mutable(ShoppingCart cart,CancellationToken ct)
    {
        var claim=await repository.ActiveClaim(cart.Id,ct);if(claim is null)return;
        if(claim.ExpiresAt>Now)throw new BusinessFault("CheckoutAlreadyInProgress");
        claim.Status=CartClaimStatus.Expired;await repository.Commit("cart.checkout.expired",claim.Id,new{OrderId=claim.Id,claim.CartId},ct);
    }
    private async Task<CartClaimView> ClaimView(CartCheckoutClaim claim,CancellationToken ct)
    {
        var preparation=await repository.Preparation(claim.PreparationId,ct)??throw new BusinessFault("PreparationNotFound",404);
        var status=claim.Status==CartClaimStatus.Held&&claim.ExpiresAt<=Now?CartClaimStatus.Expired:claim.Status;
        return new(claim.Id,status,claim.ExpiresAt,Convert.ToBase64String(claim.Version),ContractJson.Read<PreparedCartView>(preparation.Document));
    }
    public async Task<CartClaimView> Claim(Guid tenant,CartClaimRequest request,string? capability,CancellationToken ct)
    {
        await access.Machine(tenant,ct);Id(request.OrderId);Id(request.PreparationId);
        return await repository.Transaction(async token=>
        {
            var prior=await repository.Claim(request.OrderId,token);
            if(prior is not null){var original=await repository.Preparation(prior.PreparationId,token);if(prior.PreparationId!=request.PreparationId||prior.UserId!=request.UserId||original?.CustomerRole!=request.TenantRole)throw new BusinessFault("CartClaimKeyReused");return await ClaimView(prior,token);}
            var preparation=await ForOrder(tenant,request.PreparationId,new(request.UserId,request.TenantRole),capability,token);
            var cart=await repository.Cart(preparation.CartId,token)??throw new BusinessFault("CartNotFound",404);Active(cart);await Mutable(cart,token);
            if(cart.Revision!=preparation.CartRevision)throw new BusinessFault("PreparationExpiredOrCartChanged");
            var value=new CartCheckoutClaim{TenantId=tenant,Id=request.OrderId,CartId=cart.Id,PreparationId=request.PreparationId,UserId=request.UserId,CartRevision=cart.Revision,CreatedAt=Now,ExpiresAt=Now.AddMinutes(15)};
            repository.Add(value);await repository.Commit("cart.checkout.claimed",value.Id,new{OrderId=value.Id,value.CartId,value.CartRevision},token);return await ClaimView(value,token);
        },ct);
    }
    public async Task<CartClaimView> OrderClaim(Guid tenant,Guid order,CancellationToken ct)
    {
        // Existing claims remain inspectable by the scoped recovery actor after tenant suspension.
        access.Recovery(tenant);return await ClaimView(await repository.Claim(order,ct)??throw new BusinessFault("CartClaimNotFound",404),ct);
    }
    public async Task<CartClaimView> CloseClaim(Guid tenant,Guid order,bool complete,string version,CancellationToken ct)
    {
        if(complete)await access.Machine(tenant,ct);else access.Recovery(tenant);
        return await repository.Transaction(async token=>
        {
            var claim=await repository.Claim(order,token)??throw new BusinessFault("CartClaimNotFound",404);var target=complete?CartClaimStatus.Completed:CartClaimStatus.Released;
            if(claim.Status==target||claim.Status==CartClaimStatus.Expired)return await ClaimView(claim,token);
            if(claim.Status!=CartClaimStatus.Held)throw new BusinessFault("CartClaimAlreadyClosed");repository.ClaimVersion(claim,version);
            if(claim.ExpiresAt<=Now)claim.Status=CartClaimStatus.Expired;
            else
            {
                var cart=await repository.Cart(claim.CartId,token)??throw new BusinessFault("CartNotFound",404);Active(cart);
                if(cart.Revision!=claim.CartRevision)throw new BusinessFault("ClaimedCartChanged");
                claim.Status=target;cart.Revision++;cart.UpdatedAt=Now;
                if(complete){cart.Status=CartStatus.CheckedOut;if(await repository.Session(cart.Id,token) is {} session)session.RevokedAt=Now;}
            }
            await repository.Commit("cart.checkout."+claim.Status.ToString().ToLowerInvariant(),order,new{OrderId=order,claim.CartId,claim.Status},token);return await ClaimView(claim,token);
        },ct);
    }
}
