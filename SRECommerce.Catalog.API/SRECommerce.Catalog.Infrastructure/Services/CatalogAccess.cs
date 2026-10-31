using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Application;
namespace SRECommerce.Catalog.Infrastructure;
public sealed class CatalogAccess(ModuleAccess access):ICatalogAccess
{
    public Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct)=>access.AuthorizeAsync(tenant,permission,ct);
    public async Task Public(Guid tenant,CancellationToken ct)=>_ = await access.PublicAsync(tenant,ct);
    public void Machine(Guid tenant,string[] sources)=>access.MachineAsync(tenant,sources);
}
