using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Application;

public sealed record AddressInput(string Label, string FullName, string Email, string? Phone,
    string AddressLine1, string? AddressLine2, string City, string CountryCode, string PostalCode, string? Version);
public sealed record AddressView(Guid Id, string Label, string FullName, string Email, string? Phone,
    string AddressLine1, string? AddressLine2, string City, string CountryCode, string PostalCode, string Version);
public sealed record AddressVersion(string Version);
public sealed record SavedProductInput(bool Active, string? Version);
public sealed record SavedProductView(Guid ProductId, SavedProductKind Kind, bool Available,
    string? Name, string? Slug, DateTimeOffset UpdatedAt, string Version);
public sealed record SavedProductPage(SavedProductView[] Items, int Page, bool HasMore);
