using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantPlatformAudit
{
    public long Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
