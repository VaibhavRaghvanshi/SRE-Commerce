using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;

public interface ITenantContext { Guid? TenantId { get; } }
