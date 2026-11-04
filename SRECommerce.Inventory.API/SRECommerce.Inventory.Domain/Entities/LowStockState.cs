using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Inventory.Domain;

public sealed class LowStockState : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid VariantId { get; set; }
    public bool Low { get; set; }
    public long Episode { get; set; }
    public decimal Available { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset? NotifiedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
