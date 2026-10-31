using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class PlanFeature
{
    public string PlanCode { get; set; } = "";
    public string Feature { get; set; } = "";
    public bool Enabled { get; set; }
}
