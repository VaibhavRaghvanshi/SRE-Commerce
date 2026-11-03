using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Discovery.Domain;

public sealed class SearchDocument : ITenantRecord
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public string SearchText { get; set; } = "";
    public string Attributes { get; set; } = "{}";
    public DateTimeOffset IndexedAt { get; set; }
}

public enum ReviewState { Pending, Approved, Rejected }
public sealed class ProductReview : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Body { get; set; } = "";
    public ReviewState State { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class ReviewDecision : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid ModeratorId { get; set; }
    public ReviewState State { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
