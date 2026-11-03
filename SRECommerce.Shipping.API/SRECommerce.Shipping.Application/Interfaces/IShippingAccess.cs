using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Shipping.Application;
public interface IShippingAccess
{
 UserAccess? Actor{get;} Task Human(Guid tenant,ActorPermission permission,CancellationToken ct); Task Public(Guid tenant,bool write,CancellationToken ct);Task Machine(Guid tenant,bool recovery,CancellationToken ct);Task<string> Currency(Guid tenant,CancellationToken ct);
}
