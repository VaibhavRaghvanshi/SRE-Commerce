namespace SRECommerce.Pricing.Application;
public sealed record PriceImportRequest(Guid Key,string Source,PriceRequest[] Prices);
