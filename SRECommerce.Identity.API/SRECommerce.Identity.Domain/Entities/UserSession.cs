namespace SRECommerce.Identity.Domain;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string AuthorizationId { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public bool MfaAuthenticated { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public DateTimeOffset? RevokedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
