using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class PriceEngine(ITaxCalculator tax)
{
    public PriceCalculation Calculate(IReadOnlyList<PriceInput> items,IReadOnlyList<PriceOffer> offers,int digits)
    {
        PricingRules.Precision(digits);if(items.Count is <1 or >100||items.Select(i=>i.VariantId).Distinct().Count()!=items.Count)throw new DomainFault("InvalidQuoteItems");
        foreach(var item in items){PricingRules.Quantity(item.Quantity);PricingRules.Money(item.Mrp);PricingRules.Money(item.SellingPrice);PricingRules.Tax(item.TaxRate);if(item.SellingPrice>item.Mrp)throw new DomainFault("SellingPriceExceedsMrp");}
        var basis=items.Select(i=>PricingRules.Round(i.SellingPrice*i.Quantity,digits)).ToArray();
        if(basis.Any(value=>value>1000000000000m)||basis.Sum()>1000000000000m)throw new DomainFault("QuoteLimitExceeded");
        var result=Build(items,basis,new decimal[items.Count],null,digits);
        foreach(var offer in offers.OrderByDescending(o=>o.Priority).ThenBy(o=>o.Id))
        {
            PricingRules.Offer(offer);if(basis.Sum()<offer.MinimumSpend)continue;
            var eligible=items.Select((item,index)=>(item,index)).Where(x=>(offer.VariantIds.Count==0||offer.VariantIds.Contains(x.item.VariantId))&&(offer.Kind!=PromotionKind.BundlePercentage||x.item.IsBundle)).Select(x=>x.index).ToHashSet();
            var weights=items.Select((item,index)=>!eligible.Contains(index)?0:offer.Kind switch
            {
                PromotionKind.Percentage or PromotionKind.BundlePercentage=>basis[index]*offer.Value/100,
                PromotionKind.FixedAmount=>basis[index],
                PromotionKind.BuyGet=>Math.Min(basis[index],decimal.Floor(item.Quantity/(offer.BuyQuantity+offer.GetQuantity))*offer.GetQuantity*item.SellingPrice),
                _=>throw new DomainFault("UnknownPromotion")
            }).ToArray();
            var total=offer.Kind==PromotionKind.FixedAmount?Math.Min(offer.Value,weights.Sum()):weights.Sum();total=PricingRules.Round(Math.Min(total,basis.Sum()),digits);if(offer.MaximumDiscount is {} cap){var factor=digits switch{0=>1m,1=>10m,2=>100m,_=>1000m};total=Math.Min(total,decimal.Floor(cap*factor)/factor);}
            if(total<=0||weights.Sum()==0)continue;
            var allocation=Allocate(total,weights,basis,digits);var candidate=Build(items,basis,allocation,offer,digits);
            if(candidate.Total<result.Total)result=candidate;
        }
        return result;
    }
    private PriceCalculation Build(IReadOnlyList<PriceInput> items,decimal[] basis,decimal[] discount,PriceOffer? offer,int digits)
    {
        var lines=items.Select((item,index)=>
        {
            var before=tax.Calculate(basis[index],item.TaxRate,item.TaxInclusive,digits);var after=tax.Calculate(basis[index]-discount[index],item.TaxRate,item.TaxInclusive,digits);
            return new CalculatedLine(item.VariantId,item.Sku,item.PriceId,item.PriceVersion,item.Quantity,item.Mrp,item.SellingPrice,before.Total,before.Total-after.Total,after.Net,after.Tax,after.Total,item.TaxRate,item.TaxInclusive);
        }).ToArray();
        return new(offer?.Id,offer?.Code,lines.Sum(l=>l.Subtotal),lines.Sum(l=>l.Discount),lines.Sum(l=>l.Net),lines.Sum(l=>l.Tax),lines.Sum(l=>l.Total),lines);
    }
    private static decimal[] Allocate(decimal total,decimal[] weights,decimal[] basis,int digits)
    {
        var factor=digits switch{0=>1m,1=>10m,2=>100m,_=>1000m};var units=total*factor;var sum=weights.Sum();var allocated=weights.Select(w=>decimal.Floor(units*w/sum)).ToArray();
        var remainder=units-allocated.Sum();var order=Enumerable.Range(0,weights.Length).Where(i=>weights[i]>0).OrderByDescending(i=>units*weights[i]/sum-allocated[i]).ThenBy(i=>i).ToArray();
        foreach(var index in order){if(remainder<=0)break;if(allocated[index]+1<=basis[index]*factor){allocated[index]++;remainder--;}}
        if(remainder!=0)throw new DomainFault("DiscountAllocationFailed",500);
        return allocated.Select((value,index)=>{var amount=value/factor;if(amount<0||amount>basis[index])throw new DomainFault("DiscountExceedsPrice",500);return amount;}).ToArray();
    }
}
