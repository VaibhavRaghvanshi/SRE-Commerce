using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Cart.Domain;

public sealed class CustomerAddress : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
