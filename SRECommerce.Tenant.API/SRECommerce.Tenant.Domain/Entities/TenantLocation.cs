using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class TenantLocation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string CountryCode { get; set; } = "IN";
    public bool IsActive { get; set; } = true;
}
