using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record EntitlementView(Guid TenantId,bool Active,string PlanCode,DateTimeOffset EndsAt,int MaxLocations,int MaxDomains,int MaxUsers,IReadOnlyCollection<ModuleView> Modules);
