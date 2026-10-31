using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public sealed partial class PricingService
{
    private static PriceOffer Offer(Domain.Promotion p)=>new(p.Id,p.Code,p.Kind,p.Value,p.BuyQuantity,p.GetQuantity,p.MinimumSpend,p.MaximumDiscount,p.Priority,ContractJson.Read<Guid[]>(p.VariantIds).ToHashSet());
    public async Task<object> Promotion(Guid tenant,Guid? id,PromotionRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);PricingRules.Window(request.StartsAt,request.EndsAt);if(request.VariantIds is null||request.VariantIds.Distinct().Count()!=request.VariantIds.Length)throw new BusinessFault("InvalidPromotionTargets",400);
        PricingRules.Offer(new(id??Guid.NewGuid(),request.Code,request.Kind,request.Value,request.BuyQuantity,request.GetQuantity,request.MinimumSpend,request.MaximumDiscount,request.Priority,request.VariantIds.ToHashSet()));
        return await repository.Transaction(async token=>
        {
            if(request.CustomerGroupId is {} group&&!(await repository.Groups(token)).Any(g=>g.Id==group&&g.Active))throw new BusinessFault("PriceGroupUnavailable");
            var value=id is null?new Domain.Promotion{TenantId=tenant}:(await repository.Promotions(token)).SingleOrDefault(p=>p.Id==id)??throw new BusinessFault("PromotionNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            value.Code=PricingRules.Code(request.Code);value.Name=PricingRules.Text(request.Name);value.Kind=request.Kind;value.Value=request.Value;value.BuyQuantity=request.BuyQuantity;value.GetQuantity=request.GetQuantity;value.MinimumSpend=request.MinimumSpend;value.MaximumDiscount=request.MaximumDiscount;value.VariantIds=ContractJson.Write(request.VariantIds.Order().ToArray());value.CustomerGroupId=request.CustomerGroupId;value.B2BOnly=request.B2BOnly;value.Automatic=request.Automatic;value.Priority=request.Priority;value.StartsAt=request.StartsAt;value.EndsAt=request.EndsAt;value.Active=request.Active;
            await repository.Commit("pricing.promotion.saved",value.Id,new{PromotionId=value.Id,value.Active},token);return View(value);
        },ct);
    }
    public async Task<object> Coupon(Guid tenant,Guid? id,CouponRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);if(request.GlobalLimit is <1 or >1000000||request.PerCustomerLimit<0||request.PerCustomerLimit>request.GlobalLimit)throw new BusinessFault("InvalidCouponLimits",400);
        return await repository.Transaction(async token=>
        {
            if(!(await repository.Promotions(token)).Any(p=>p.Id==request.PromotionId))throw new BusinessFault("PromotionUnavailable");
            var value=id is null?new Domain.Coupon{TenantId=tenant}:(await repository.Coupons(token)).SingleOrDefault(c=>c.Id==id)??throw new BusinessFault("CouponNotFound",404);
            if(id is null)repository.Add(value);else{repository.Version(value,request.Version);if(value.PromotionId!=request.PromotionId)throw new BusinessFault("CouponPromotionImmutable");}
            if(await repository.CouponUsage(value.Id,null,false,Now,token)>request.GlobalLimit)throw new BusinessFault("CouponLimitBelowCurrentUsage");
            value.Code=PricingRules.Code(request.Code);value.PromotionId=request.PromotionId;value.GlobalLimit=request.GlobalLimit;value.PerCustomerLimit=request.PerCustomerLimit;value.Active=request.Active;
            await repository.Commit("pricing.coupon.saved",value.Id,new{CouponId=value.Id,value.Active},token);return View(value);
        },ct);
    }
    private async Task CheckCoupon(Domain.Coupon coupon,Guid? user,CancellationToken ct)
    {
        if(!coupon.Active||coupon.PerCustomerLimit>0&&user is null)throw new BusinessFault("CouponUnavailable",400);
        if(await repository.CouponUsage(coupon.Id,null,false,Now,ct)>=coupon.GlobalLimit)throw new BusinessFault("CouponUnavailable");
        if(coupon.PerCustomerLimit>0&&await repository.CouponUsage(coupon.Id,user,true,Now,ct)>=coupon.PerCustomerLimit)throw new BusinessFault("CouponUnavailable");
    }
}
