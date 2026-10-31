using System.Text;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public sealed partial class PricingService
{
    private async Task<(Guid? Id,string Hash)> CustomerGroup(Guid? user,CancellationToken ct)
    {
        if(user is null)return(null,"");var member=await repository.GroupMember(user.Value,ct);
        if(member is null||!member.Active)return(null,"");var group=(await repository.Groups(ct)).SingleOrDefault(g=>g.Id==member.GroupId&&g.Active);
        return group is null?(null,""):(group.Id,Hash(new{Member=Version(member.Version),Group=Version(group.Version)}));
    }
    public async Task<QuoteView> Quote(Guid tenant,QuoteRequest request,bool publicRead,CancellationToken ct)
    {
        if(!publicRead)await access.Authorize(tenant,ActorPermission.Customer,ct);var metadata=publicRead?await access.Guest(tenant,ct):await access.Tenant(tenant,ct);
        Id(request.Id);if(request.Items is null||request.Items.Length is <1 or >100||request.Items.Select(i=>i.VariantId).Distinct().Count()!=request.Items.Length)throw new BusinessFault("InvalidQuoteItems",400);
        foreach(var item in request.Items){Id(item.VariantId);PricingRules.Quantity(item.Quantity);}
        var ids=request.Items.Select(i=>i.VariantId).ToArray();var visible=(await access.VisibleVariants(tenant,ids,publicRead,ct)).ToHashSet();if(ids.Any(id=>!visible.Contains(id)))throw new BusinessFault("CatalogVariantUnavailable");
        var actor=publicRead?null:access.Actor;var user=actor?.UserId;var b2b=actor?.TenantRole=="B2BBuyer";
        return await repository.Transaction(async token=>
        {
            var now=Now;var settings=await Config(token);if(settings.Currency!=metadata.Currency)throw new BusinessFault("TenantCurrencyMismatch");var group=await CustomerGroup(user,token);
            var hash=Hash(new{Request=request,User=user,Role=actor?.TenantRole,Group=group.Id,Context=group.Hash});
            var previous=await repository.Quote(request.Id,token);
            if(previous is not null){if(previous.UserId!=user||previous.RequestHash!=hash)throw new BusinessFault("QuoteKeyReused");if(previous.ExpiresAt<=now)throw new BusinessFault("QuoteExpired");return ContractJson.Read<QuoteView>(previous.Document);}
            var lists=(await repository.Lists(token)).Where(l=>l.Active&&l.Currency==settings.Currency&&l.StartsAt<=now&&l.EndsAt>now&&(l.Audience==PriceAudience.Retail||l.Audience==PriceAudience.B2B&&b2b||l.Audience==PriceAudience.CustomerGroup&&l.CustomerGroupId==group.Id)).ToDictionary(l=>l.Id);
            var taxes=(await repository.Taxes(token)).Where(t=>t.Active).ToDictionary(t=>t.Id);var prices=await repository.Prices(ids,null,token);var inputs=new List<PriceInput>();var expires=now.AddMinutes(5);
            foreach(var item in request.Items)
            {
                var candidates=prices.Where(p=>p.VariantId==item.VariantId&&p.Active&&p.MinimumQuantity<=item.Quantity&&lists.ContainsKey(p.PriceListId)&&taxes.ContainsKey(p.TaxPolicyId)).GroupBy(p=>p.PriceListId).Select(g=>g.OrderByDescending(p=>p.MinimumQuantity).First());
                var price=candidates.OrderByDescending(p=>lists[p.PriceListId].Audience==PriceAudience.CustomerGroup?3:lists[p.PriceListId].Audience==PriceAudience.B2B?2:1).ThenByDescending(p=>lists[p.PriceListId].Priority).ThenBy(p=>p.SellingPrice).ThenBy(p=>p.Id).FirstOrDefault()??throw new BusinessFault("PriceUnavailable");
                var tax=taxes[price.TaxPolicyId];inputs.Add(new(price.VariantId,price.Sku,price.Id,Version(price.Version),item.Quantity,price.Mrp,price.SellingPrice,tax.Rate,tax.Inclusive,price.IsBundle));if(lists[price.PriceListId].EndsAt<expires)expires=lists[price.PriceListId].EndsAt;
            }
            var offers=(await repository.Promotions(token)).Where(p=>p.Active&&p.StartsAt<=now&&p.EndsAt>now&&(!p.B2BOnly||b2b)&&(p.CustomerGroupId is null||p.CustomerGroupId==group.Id)).ToArray();
            Domain.Coupon? coupon=null;Domain.Promotion? couponOffer=null;
            if(!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var code=PricingRules.Code(request.CouponCode);coupon=(await repository.Coupons(token)).SingleOrDefault(c=>c.Code==code)??throw new BusinessFault("CouponUnavailable",400);await CheckCoupon(coupon,user,token);couponOffer=offers.SingleOrDefault(p=>p.Id==coupon.PromotionId&&!p.Automatic)??throw new BusinessFault("CouponUnavailable",400);
                if(engine.Calculate(inputs,[Offer(couponOffer)],settings.DecimalPlaces).PromotionId is null)throw new BusinessFault("CouponNotApplicable",400);
            }
            var applicable=offers.Where(p=>p.Automatic||p.Id==couponOffer?.Id).Select(Offer).ToArray();var calculation=engine.Calculate(inputs,applicable,settings.DecimalPlaces);var applied=offers.SingleOrDefault(p=>p.Id==calculation.PromotionId);
            if(applied is not null&&applied.EndsAt<expires)expires=applied.EndsAt;var couponId=calculation.PromotionId==couponOffer?.Id?coupon?.Id:null;
            var view=new QuoteView(request.Id,tenant,user,settings.Currency,settings.DecimalPlaces,expires,couponId,calculation);var json=ContractJson.Write(view);if(Encoding.UTF8.GetByteCount(json)>131072)throw new BusinessFault("QuoteTooLarge",400);
            repository.Add(new PricingQuote{TenantId=tenant,Id=request.Id,UserId=user,CustomerGroupId=group.Id,GroupContextHash=group.Hash,CouponId=couponId,RequestHash=hash,Document=json,CreatedAt=now,ExpiresAt=expires});
            await repository.Commit("pricing.quote.created",request.Id,null,token);return view;
        },ct);
    }
    public async Task<QuoteView> QuoteById(Guid tenant,Guid id,bool machine,CancellationToken ct)
    {
        if(machine)await access.Machine(tenant,["Cart","Order"],ct);else await access.Authorize(tenant,ActorPermission.Customer,ct);
        var value=await repository.Quote(id,ct)??throw new BusinessFault("QuoteNotFound",404);
        if(!machine&&!access.Actor.IsPlatformAdmin&&value.UserId!=access.Actor.UserId)throw new BusinessFault("QuoteNotFound",404);return ContractJson.Read<QuoteView>(value.Document);
    }
    public async Task<RedemptionView> Reserve(Guid tenant,RedemptionRequest request,CancellationToken ct)
    {
        await access.Machine(tenant,["Order"],ct);Id(request.OrderId);Id(request.QuoteId);
        return await repository.Transaction(async token=>
        {
            var previous=await repository.Redemption(request.OrderId,token);
            if(previous is not null){if(previous.QuoteId!=request.QuoteId||previous.UserId!=request.UserId)throw new BusinessFault("CheckoutPriceKeyReused");if(previous.Status==RedemptionStatus.Reserved&&previous.ExpiresAt<=Now){previous.Status=RedemptionStatus.Expired;await repository.Commit("pricing.redemption.expired",previous.Id,new{OrderId=previous.Id},token);}return View(previous);}
            var quote=await repository.Quote(request.QuoteId,token)??throw new BusinessFault("QuoteNotFound",404);if(quote.UserId!=request.UserId)throw new BusinessFault("QuoteCustomerMismatch",403);if(quote.ExpiresAt<=Now)throw new BusinessFault("QuoteExpired");
            var context=await CustomerGroup(quote.UserId,token);if(context.Id!=quote.CustomerGroupId||context.Hash!=quote.GroupContextHash)throw new BusinessFault("PriceGroupChanged");
            var settings=await Config(token);var snapshot=ContractJson.Read<QuoteView>(quote.Document);if(snapshot.Currency!=settings.Currency||snapshot.DecimalPlaces!=settings.DecimalPlaces)throw new BusinessFault("QuoteCurrencyChanged");
            if(quote.CouponId is {} couponId){var coupon=(await repository.Coupons(token)).SingleOrDefault(c=>c.Id==couponId)??throw new BusinessFault("CouponUnavailable");await CheckCoupon(coupon,quote.UserId,token);}
            var value=new CouponRedemption{TenantId=tenant,Id=request.OrderId,QuoteId=quote.Id,CouponId=quote.CouponId,UserId=quote.UserId,CreatedAt=Now,ExpiresAt=Now.AddMinutes(15)};repository.Add(value);
            await repository.Commit("pricing.redemption.reserved",value.Id,new{OrderId=value.Id,value.QuoteId,value.CouponId},token);return View(value);
        },ct);
    }
    public async Task<RedemptionView> Finish(Guid tenant,Guid id,bool redeem,string version,CancellationToken ct)
    {
        await access.Machine(tenant,["Order"],ct);
        return await repository.Transaction(async token=>
        {
            var value=await repository.Redemption(id,token)??throw new BusinessFault("RedemptionNotFound",404);var target=redeem?RedemptionStatus.Redeemed:RedemptionStatus.Released;
            if(value.Status==target||value.Status==RedemptionStatus.Expired)return View(value);if(value.Status!=RedemptionStatus.Reserved)throw new BusinessFault("RedemptionAlreadyFinal");repository.Version(value,version);
            value.Status=value.ExpiresAt<=Now?RedemptionStatus.Expired:target;await repository.Commit("pricing.redemption."+value.Status.ToString().ToLowerInvariant(),value.Id,new{OrderId=value.Id,value.QuoteId,value.CouponId,Status=value.Status},token);return View(value);
        },ct);
    }
}
