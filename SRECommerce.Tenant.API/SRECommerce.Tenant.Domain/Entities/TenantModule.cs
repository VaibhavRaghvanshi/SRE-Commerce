using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantModule : ITenantRecord
{
    public Guid TenantId { get; set; }
    public string Module { get; set; } = "";
    public bool Enabled { get; set; }
}
