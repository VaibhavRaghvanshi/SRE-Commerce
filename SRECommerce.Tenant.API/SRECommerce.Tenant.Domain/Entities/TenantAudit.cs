using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantAudit : ITenantRecord
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public bool PlatformOperation { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
