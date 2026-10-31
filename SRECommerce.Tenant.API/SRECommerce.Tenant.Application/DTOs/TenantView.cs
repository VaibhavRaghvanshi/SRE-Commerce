using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record TenantView(Guid Id,Guid OwnerUserId,string Name,string Slug,string BusinessTypeCode,string? CustomBusinessType,TenantStatus Status,string? SuspensionReason,DatabaseStrategy DatabaseStrategy,string Version,DateTimeOffset CreatedAt);
