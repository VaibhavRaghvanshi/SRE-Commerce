using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Order.Application;
public interface IOrderAccess
{
 UserAccess? Actor {get;}Task Authorize(Guid tenant,bool guest,ActorPermission permission,CancellationToken ct);void Recovery(Guid tenant,string[] sources);
 Task RequireOrdering(Guid tenant,CancellationToken ct);
}
