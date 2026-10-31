namespace SRECommerce.Pricing.Application;
public record TaxRequest(string Code,string Name,decimal Rate,bool Inclusive,bool Active=true,string? Version=null);
