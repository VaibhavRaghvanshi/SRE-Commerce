using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Payment.Domain;

public sealed class GiftCard : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid IssuedBy { get; set; }
    public Guid? ClaimedBy { get; set; }
    public string CodeHash { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal Value { get; set; }
    public decimal Balance { get; set; }
    public string Reference { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
