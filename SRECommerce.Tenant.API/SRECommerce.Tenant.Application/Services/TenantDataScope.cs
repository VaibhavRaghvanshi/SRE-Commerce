using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public sealed class TenantDataScope { public Guid? TenantId { get; set; } }
