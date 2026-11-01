using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Application;
namespace SRECommerce.Catalog.Infrastructure;
public sealed class CatalogAccess(ModuleAccess access,RemoteAccess remote):ICatalogAccess
{
    public bool CanViewB2B=>access.User.TenantRole is "B2BBuyer" or "Owner" or "Manager";
    public Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct)=>access.AuthorizeAsync(tenant,permission,ct);
    public async Task Public(Guid tenant,CancellationToken ct)=>_ = await access.PublicAsync(tenant,ct);
    public async Task Machine(Guid tenant,string[] sources,CancellationToken ct)
    {access.MachineAsync(tenant,sources);var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||!state.Modules.Contains("catalog"))throw new BusinessFault("CatalogUnavailable",403);}
}
