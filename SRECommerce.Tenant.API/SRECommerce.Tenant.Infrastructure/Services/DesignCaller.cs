using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;
public sealed class DesignCaller : ITenantCaller { public TenantCaller Current => new(Guid.Empty,"",false,null,null); }
