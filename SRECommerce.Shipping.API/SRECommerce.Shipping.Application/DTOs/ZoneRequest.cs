namespace SRECommerce.Shipping.Application;
public sealed record ZoneRequest(Guid OperationId,string Code,string Name,string[] Countries,string[] PostalPrefixes,bool Active=true,string? Version=null);
