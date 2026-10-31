using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Tenant.Application;
public interface ITenantRegistry
{
    Task<PublicTenant?> PublicById(Guid id,CancellationToken ct);
    Task<PublicTenant?> PublicBySlug(string slug,CancellationToken ct);
    Task<ProvisioningLimits?> Limits(Guid id,CancellationToken ct);
}
