using System.Net;using System.Net.Http.Json;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class ShippingDependencies(ServiceHttp transport,RemoteAccess remote):IShippingDependencies
{
 private async Task<T> Send<T>(string target,HttpMethod method,string path,object? body,CancellationToken ct,string? cartKey=null)
 {
  using var request=new HttpRequestMessage(method,path);if(body is not null)request.Content=JsonContent.Create(body);if(!string.IsNullOrEmpty(cartKey))request.Headers.Add("X-Cart-Key",cartKey);
  using var response=await transport.Send(target,request,ct);if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Gone)throw new BusinessFault(target+"ShippingRejected",(int)response.StatusCode);
  if(response.StatusCode==HttpStatusCode.Unauthorized)throw new BusinessFault("ShippingProofRequired",401);if(!response.IsSuccessStatusCode)throw new DependencyFault(target);return await RemoteAccess.Read<T>(response,target,ct)??throw new DependencyFault(target);
 }
 public Task<PreparedShippingCart> Preparation(Guid tenant,Guid id,Guid? user,string? role,string? cartKey,CancellationToken ct)=>Send<PreparedShippingCart>("Cart",HttpMethod.Post,$"internal/v1/tenants/{tenant}/cart/preparations/{id}/shipping-verify",new{UserId=user,TenantRole=role},ct,cartKey);
 public Task<WarehouseProof> Warehouse(Guid tenant,Guid id,CancellationToken ct)=>Send<WarehouseProof>("Inventory",HttpMethod.Get,$"internal/v1/tenants/{tenant}/inventory/warehouses/{id}",null,ct);
 public Task<WarehouseProof[]> Warehouses(Guid tenant,Guid[] ids,CancellationToken ct)=>Send<WarehouseProof[]>("Inventory",HttpMethod.Get,$"internal/v1/tenants/{tenant}/inventory/warehouses?"+string.Join("&",ids.Select(id=>"ids="+id)),null,ct);
 public Task<ShipmentContextProof> Fulfillment(Guid tenant,Guid id,CancellationToken ct)=>Send<ShipmentContextProof>("Order",HttpMethod.Get,$"internal/v1/tenants/{tenant}/orders/fulfillments/{id}",null,ct);
 public async Task UpdateFulfillment(Guid tenant,Guid id,OrderShipmentUpdate update,CancellationToken ct)=>_=await Send<object>("Order",HttpMethod.Post,$"internal/v1/tenants/{tenant}/orders/fulfillments/{id}/status",update,ct);
 public Task<ReturnPickupContext> Return(Guid tenant,Guid id,CancellationToken ct)=>Send<ReturnPickupContext>("Order",HttpMethod.Get,$"internal/v1/tenants/{tenant}/orders/returns/{id}",null,ct);
 public async Task VerifyCustomerOrder(Guid tenant,Guid id,bool guest,string? orderKey,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/{(guest?"public/":"")}tenants/{tenant}/orders/{id}");
  if(guest){if(!string.IsNullOrEmpty(orderKey))request.Headers.Add("X-Order-Key",orderKey);}else request.Headers.Authorization=new("Bearer",remote.UserBearer());
  using var response=await remote.Send("order",request,ct);if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)throw new BusinessFault("OrderTrackingAccessDenied",(int)response.StatusCode);if(!response.IsSuccessStatusCode)throw new DependencyFault("Order");
 }
}
