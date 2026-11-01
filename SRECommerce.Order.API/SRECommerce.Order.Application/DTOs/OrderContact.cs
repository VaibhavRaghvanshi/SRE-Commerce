using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderContact(string FullName,string Email,string? Phone,string AddressLine1,string? AddressLine2,string City,string CountryCode,string PostalCode);
