using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Catalog.Application;
public interface ICatalogAccess
{
    bool CanViewB2B {get;}
    Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct);
    Task Public(Guid tenant,CancellationToken ct);
    void Machine(Guid tenant,string[] sources);
}
