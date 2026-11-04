using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Notification.Domain;

public sealed class StockAlertSubscription : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool Enabled { get; set; }
    public int CooldownMinutes { get; set; } = 1440;
    public byte[] Version { get; set; } = [];
}

public sealed class StockAlertReceipt : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid VariantId { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
}
