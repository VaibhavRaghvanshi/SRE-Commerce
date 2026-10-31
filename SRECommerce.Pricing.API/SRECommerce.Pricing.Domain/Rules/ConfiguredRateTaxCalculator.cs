namespace SRECommerce.Pricing.Domain;
// Deterministic local adapter for merchant-configured tax metadata. It does not infer statutory tax rules.
public sealed class ConfiguredRateTaxCalculator:ITaxCalculator
{
    public TaxBreakdown Calculate(decimal amount,decimal rate,bool inclusive,int digits)
    {
        PricingRules.Tax(rate);amount=PricingRules.Round(amount,digits);
        var net=inclusive?PricingRules.Round(amount/(1+rate/100),digits):amount;
        var tax=inclusive?amount-net:PricingRules.Round(net*rate/100,digits);
        return new(net,tax,net+tax);
    }
}
