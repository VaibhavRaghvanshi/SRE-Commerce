using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public interface ITenantRecord { Guid TenantId { get; set; } }
