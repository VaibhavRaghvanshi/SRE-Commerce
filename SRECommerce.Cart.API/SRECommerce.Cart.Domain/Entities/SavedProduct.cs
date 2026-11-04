using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Cart.Domain;

public enum SavedProductKind { Wishlist, RecentlyViewed }

public sealed class SavedProduct : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public SavedProductKind Kind { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
