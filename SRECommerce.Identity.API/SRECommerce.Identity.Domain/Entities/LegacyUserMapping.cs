namespace SRECommerce.Identity.Domain;

public sealed class LegacyUserMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = "";
    public int LegacyId { get; set; }
    public Guid UserId { get; set; }
    public string LegacyRole { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}
