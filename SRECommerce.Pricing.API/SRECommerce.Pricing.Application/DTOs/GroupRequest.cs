namespace SRECommerce.Pricing.Application;
public record GroupRequest(string Code,string Name,bool Active=true,string? Version=null);
