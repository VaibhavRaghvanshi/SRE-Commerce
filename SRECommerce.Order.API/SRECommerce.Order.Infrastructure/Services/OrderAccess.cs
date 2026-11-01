using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Order.Application;
namespace SRECommerce.Order.Infrastructure;
public sealed class OrderAccess(ModuleAccess access,ModuleScope scope,RemoteAccess remote):IOrderAccess
{
 public UserAccess? Actor=>scope.User;
 public async Task Authorize(Guid tenant,bool guest,ActorPermission permission,CancellationToken ct){if(guest){await access.PublicAsync(tenant,ct);scope.BindGuest(tenant);}else await access.AuthorizeAsync(tenant,permission,ct);}
 public void Recovery(Guid tenant,string[] sources)=>access.MachineAsync(tenant,sources);
 public async Task RequireOrdering(Guid tenant,CancellationToken ct){var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||state.TenantId!=tenant||!new[]{"orders","cart","catalog","inventory","pricing","shipping","payments"}.All(state.Modules.Contains))throw new BusinessFault("CheckoutModulesUnavailable",403);}
}
