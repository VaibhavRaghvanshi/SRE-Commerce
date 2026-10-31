using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public interface IIdentityOwnerProvisioner { Task ProvisionOwnerAsync(Guid tenantId,Guid userId,CancellationToken ct); }
