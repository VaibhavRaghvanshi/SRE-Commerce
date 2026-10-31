using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class SubscriptionPlan
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int MaxLocations { get; set; }
    public int MaxDomains { get; set; }
    public int MaxUsers { get; set; }
}
