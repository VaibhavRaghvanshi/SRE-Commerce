using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record DomainView(Guid Id,string Host,DateTimeOffset? VerifiedAt,bool IsPrimary,bool IsActive);
