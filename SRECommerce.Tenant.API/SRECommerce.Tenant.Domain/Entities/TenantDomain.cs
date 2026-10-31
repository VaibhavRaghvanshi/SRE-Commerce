using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantDomain : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Host { get; set; } = "";
    public string VerificationHash { get; set; } = "";
    public DateTimeOffset VerificationExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}
