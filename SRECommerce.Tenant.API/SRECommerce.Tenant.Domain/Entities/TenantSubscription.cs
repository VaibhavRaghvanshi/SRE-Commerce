using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantSubscription : ITenantRecord
{
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = "";
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsEffective(DateTimeOffset now) => Status is SubscriptionStatus.Trial or SubscriptionStatus.Active && StartsAt<=now && EndsAt>now;
}
