namespace SRECommerce.Shipping.Application;
public sealed record ZoneView(Guid Id,string Code,string Name,string[] Countries,string[] PostalPrefixes,bool Active,string Version);
