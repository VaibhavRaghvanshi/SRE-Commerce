using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public sealed class NoTenantContext : ITenantContext { public Guid? TenantId => null; }
