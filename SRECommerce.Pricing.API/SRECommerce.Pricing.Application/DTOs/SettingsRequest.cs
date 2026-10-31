namespace SRECommerce.Pricing.Application;
public record SettingsRequest(string Currency,int DecimalPlaces,string? Version=null);
