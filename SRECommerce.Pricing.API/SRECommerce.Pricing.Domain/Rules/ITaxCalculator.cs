namespace SRECommerce.Pricing.Domain;
public interface ITaxCalculator { TaxBreakdown Calculate(decimal amount,decimal rate,bool inclusive,int digits); }
