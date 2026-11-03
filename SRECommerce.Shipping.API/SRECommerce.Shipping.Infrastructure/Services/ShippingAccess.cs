using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class ShippingAccess(ModuleAccess access,ModuleScope scope,RemoteAccess remote):IShippingAccess
{
 public UserAccess? Actor=>scope.User;public Task Human(Guid tenant,ActorPermission permission,CancellationToken ct)=>access.AuthorizeAsync(tenant,permission,ct);
 public async Task Public(Guid tenant,bool write,CancellationToken ct){await access.PublicAsync(tenant,ct);if(write)scope.BindGuest(tenant);}
 public async Task Machine(Guid tenant,bool recovery,CancellationToken ct){if(!recovery)await access.PublicAsync(tenant,ct);access.MachineAsync(tenant,"Order");}
 public async Task<string> Currency(Guid tenant,CancellationToken ct){var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||state.TenantId!=tenant||!state.Modules.Contains("shipping"))throw new BusinessFault("ShippingUnavailable",403);return state.Currency;}
}
