namespace SRECommerce.Identity.Domain;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public AccountKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) throw new ArgumentException("A name of 1-160 characters is required.");
        FullName = name.Trim();
    }
}
