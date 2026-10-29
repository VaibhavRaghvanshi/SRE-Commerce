namespace SRECommerce.Identity.Domain;

public enum AccountKind { Customer, BusinessOwner, Staff, SuperAdmin, B2BBuyer }

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

public sealed class TenantMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] Version { get; set; } = [];
}

public sealed class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "";
    public string Permission { get; set; } = "";
}

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

public sealed class SecurityAuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = "";
    public string Outcome { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
}

public sealed class LegacyUserMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = "";
    public int LegacyId { get; set; }
    public Guid UserId { get; set; }
    public string LegacyRole { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class TenantRoles
{
    public static readonly string[] All = ["Owner", "Manager", "Staff", "Customer", "B2BBuyer"];
    public static string[] Permissions(string role) => role switch
    {
        "Owner" or "Manager" => ["members.read", "members.write"],
        "Staff" => ["members.read"],
        _ => []
    };
}
