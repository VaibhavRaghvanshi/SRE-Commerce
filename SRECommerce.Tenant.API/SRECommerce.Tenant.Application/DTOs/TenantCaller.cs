using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;

public record TenantCaller(Guid UserId,string AccountKind,bool IsPlatformAdmin,Guid? SelectedTenantId,string? TenantRole);
