using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Cart.Domain;

public sealed class CartReminder : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }
    public ShoppingCart Cart { get; set; } = null!;
    public DateTimeOffset RequestedAt { get; set; }
    public bool Enabled { get; set; }
    public Guid? QueuedEventId { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
