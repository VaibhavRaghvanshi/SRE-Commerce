namespace SRECommerce.Shipping.Application;
public sealed record ShippingContact(string FullName,string Email,string? Phone,string AddressLine1,string? AddressLine2,string City,string CountryCode,string PostalCode);
