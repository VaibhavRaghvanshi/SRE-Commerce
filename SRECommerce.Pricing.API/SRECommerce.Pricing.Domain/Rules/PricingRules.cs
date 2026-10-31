using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public static partial class PricingRules
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{0,63}$")]private static partial Regex CodePattern();
    public static string Text(string value,int max=160)
    {if(string.IsNullOrWhiteSpace(value)||value.Length>max||value.Any(char.IsControl)||value.Contains('<')||value.Contains('>'))throw new DomainFault("InvalidPricingText");return value.Trim();}
    public static string Code(string value)
    {var code=Text(value,64).ToUpperInvariant();if(!CodePattern().IsMatch(code))throw new DomainFault("InvalidPricingCode");return code;}
    public static string Currency(string value)
    {if(value.Length!=3||value.Any(c=>c is <'A' or >'Z'))throw new DomainFault("InvalidPriceCurrency");return value;}
    public static decimal Money(decimal value)
    {if(value<0||value>1000000000m||decimal.Round(value,4)!=value)throw new DomainFault("InvalidPriceAmount");return value;}
    public static decimal Quantity(decimal value)
    {if(value<=0||value>1000000m||decimal.Round(value,3)!=value)throw new DomainFault("InvalidPriceQuantity");return value;}
    public static void Window(DateTimeOffset start,DateTimeOffset end)
    {if(end<=start||end-start>TimeSpan.FromDays(3660))throw new DomainFault("InvalidPricingWindow");}
    public static int Precision(int digits)
    {if(digits is <0 or >3)throw new DomainFault("InvalidCurrencyPrecision");return digits;}
    public static decimal Round(decimal value,int digits)=>decimal.Round(value,Precision(digits),MidpointRounding.AwayFromZero);
    public static void Tax(decimal rate)
    {if(rate<0||rate>100||decimal.Round(rate,4)!=rate)throw new DomainFault("InvalidTaxRate");}
    public static void Offer(PriceOffer offer)
    {
        if(!Enum.IsDefined(offer.Kind)||offer.MinimumSpend<0||offer.MaximumDiscount<0||offer.Priority is <-1000 or >1000)throw new DomainFault("InvalidPromotion");
        Money(offer.Value);Money(offer.MinimumSpend);if(offer.MaximumDiscount is {} maximum)Money(maximum);
        if(offer.Kind==PromotionKind.BuyGet){if(offer.BuyQuantity is <1 or >1000||offer.GetQuantity is <1 or >1000||offer.Value!=0)throw new DomainFault("InvalidBuyGetOffer");}
        else if(offer.Value<=0||offer.Kind is PromotionKind.Percentage or PromotionKind.BundlePercentage&&offer.Value>100)throw new DomainFault("InvalidPromotionValue");
        if(offer.VariantIds.Count>100||offer.VariantIds.Any(id=>id==Guid.Empty))throw new DomainFault("InvalidPromotionTargets");
    }
}
