using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Pricing.Domain;
using Xunit;
namespace SRECommerce.Pricing.Tests;
public sealed class PriceEngineTests
{
    private readonly PriceEngine engine=new(new ConfiguredRateTaxCalculator());
    private static PriceInput Item(decimal price=100,decimal quantity=1,decimal rate=0,bool inclusive=false,bool bundle=false)=>new(Guid.NewGuid(),"SKU-1",Guid.NewGuid(),"version",quantity,price,price,rate,inclusive,bundle);
    private static PriceOffer Offer(PromotionKind kind,decimal value=0,int buy=0,int get=0,decimal? cap=null)=>new(Guid.NewGuid(),"OFFER",kind,value,buy,get,0,cap,0,new HashSet<Guid>());
    [Theory][InlineData(false,"100","18","118")][InlineData(true,"84.75","15.25","100")]
    public void Inclusive_and_exclusive_tax_preserve_total_invariants(bool inclusive,string net,string tax,string total)
    {
        var result=engine.Calculate([Item(rate:18,inclusive:inclusive)],[],2);
        Assert.Equal(decimal.Parse(net,System.Globalization.CultureInfo.InvariantCulture),result.Net);Assert.Equal(decimal.Parse(tax,System.Globalization.CultureInfo.InvariantCulture),result.Tax);Assert.Equal(decimal.Parse(total,System.Globalization.CultureInfo.InvariantCulture),result.Total);Assert.Equal(result.Total,result.Net+result.Tax);
    }
    [Fact]public void Best_discount_is_selected_without_stacking()
    {var percent=Offer(PromotionKind.Percentage,10);var amount=Offer(PromotionKind.FixedAmount,15);var result=engine.Calculate([Item()],[percent,amount],2);Assert.Equal(85,result.Total);Assert.Equal(amount.Id,result.PromotionId);Assert.Equal(15,result.Discount);}
    [Fact]public void Bundle_discount_does_not_discount_unrelated_simple_items()
    {var result=engine.Calculate([Item(bundle:true),Item()],[Offer(PromotionKind.BundlePercentage,20)],2);Assert.Equal(180,result.Total);Assert.Equal(20,result.Lines[0].Discount);Assert.Equal(0,result.Lines[1].Discount);}
    [Theory][InlineData(1,0)][InlineData(2,0)][InlineData(3,100)][InlineData(6,200)][InlineData(7,200)]
    public void Buy_two_get_one_requires_actual_qualifying_quantity(int quantity,int discount)
    {var result=engine.Calculate([Item(quantity:quantity)],[Offer(PromotionKind.BuyGet,buy:2,get:1)],2);Assert.Equal(discount,result.Discount);}
    [Fact]public void Fixed_discount_is_capped_and_allocated_without_creating_money()
    {var result=engine.Calculate([Item(0.01m),Item(0.01m),Item(0.01m)],[Offer(PromotionKind.FixedAmount,1)],2);Assert.Equal(0,result.Total);Assert.Equal(0.03m,result.Discount);Assert.All(result.Lines,l=>Assert.True(l.Total>=0));}
    [Fact]public void Maximum_discount_never_rounds_up_past_the_merchant_cap()
    {var result=engine.Calculate([Item()],[Offer(PromotionKind.Percentage,50,cap:1.005m)],2);Assert.Equal(1,result.Discount);Assert.Equal(99,result.Total);}
    [Fact]public void Fractional_quantities_and_three_decimal_currency_are_exact()
    {var result=engine.Calculate([Item(1.234m,1.5m)],[],3);Assert.Equal(1.851m,result.Total);}
    [Fact]public void Zero_decimal_currency_uses_configured_rounding()
    {var result=engine.Calculate([Item(100.5m)],[],0);Assert.Equal(101,result.Total);}
    [Theory][InlineData("-1")][InlineData("0")][InlineData("0.0001")][InlineData("1000001")]
    public void Invalid_quantities_fail_before_calculation(string value)=>Assert.Throws<DomainFault>(()=>engine.Calculate([Item(quantity:decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture))],[],2));
    [Fact]public void Client_discount_cannot_exceed_the_price_or_use_an_unknown_rule()
    {Assert.Throws<DomainFault>(()=>engine.Calculate([Item()],[Offer(PromotionKind.Percentage,101)],2));Assert.Throws<DomainFault>(()=>engine.Calculate([Item()],[Offer((PromotionKind)99,10)],2));}
    [Fact]public void Minimum_spend_and_target_variant_are_enforced()
    {var item=Item();var offer=Offer(PromotionKind.Percentage,10) with{MinimumSpend=200};Assert.Equal(100,engine.Calculate([item],[offer],2).Total);offer=offer with{MinimumSpend=0,VariantIds=new HashSet<Guid>{Guid.NewGuid()}};Assert.Equal(100,engine.Calculate([item],[offer],2).Total);}
    [Fact]public void Huge_quotes_are_rejected_before_loss_of_display_precision()
    {Assert.Throws<DomainFault>(()=>engine.Calculate([Item(1000000000,1000000)],[],2));}
}
