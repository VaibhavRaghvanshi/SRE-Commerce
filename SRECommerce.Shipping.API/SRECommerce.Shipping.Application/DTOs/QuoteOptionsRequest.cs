namespace SRECommerce.Shipping.Application;
public sealed record QuoteOptionsRequest(Guid Id,Guid PreparationId,string CountryCode,string PostalCode);
