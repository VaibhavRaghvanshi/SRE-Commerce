using System.Net;using System.Net.Http.Json;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Order.Application;
namespace SRECommerce.Order.Infrastructure;
public sealed class OrderDependencies(ServiceHttp transport):IOrderDependencies
{
 private async Task<T?> Send<T>(string target,HttpMethod method,string path,object? body,CancellationToken ct,bool missing=false,string? cartCapability=null)
 {
  using var request=new HttpRequestMessage(method,path);if(body is not null)request.Content=JsonContent.Create(body);if(!string.IsNullOrEmpty(cartCapability))request.Headers.Add("X-Cart-Key",cartCapability);
  using var response=await transport.Send(target,request,ct);if(missing&&response.StatusCode==HttpStatusCode.NotFound)return default;
  if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone)throw new BusinessFault(target+"Rejected",(int)response.StatusCode);
  if(!response.IsSuccessStatusCode)throw new DependencyFault(target);return await RemoteAccess.Read<T>(response,target,ct)??throw new DependencyFault(target);
 }
 private static string Base(Guid tenant,string name)=>$"internal/v1/tenants/{tenant}/{name}";
 public async Task AbortCart(Guid tenant,Guid order,Guid preparation,Guid? user,string? role,CancellationToken ct)=>_=await Send<object>("Cart",HttpMethod.Post,Base(tenant,"cart/claims/cancel"),new{OrderId=order,PreparationId=preparation,UserId=user,TenantRole=role},ct);
 public async Task AbortPrice(Guid tenant,Guid order,Guid quote,Guid? user,CancellationToken ct)=>_=await Send<object>("Pricing",HttpMethod.Post,Base(tenant,"pricing/redemptions/cancel"),new{OrderId=order,QuoteId=quote,UserId=user},ct);
 public async Task AbortStock(Guid tenant,Guid reservation,Guid operation,CancellationToken ct)=>_=await Send<object>("Inventory",HttpMethod.Post,Base(tenant,$"inventory/reservations/{reservation}/cancel"),new{OperationId=operation,Reason="Checkout compensation"},ct);
 public async Task<RemotePayment> AbortPayment(Guid tenant,Guid order,CancellationToken ct)=>(await Send<RemotePayment>("Payment",HttpMethod.Post,Base(tenant,$"payments/orders/{order}/abort"),new{},ct))!;
 public async Task<SnapshotCartClaim> ClaimCart(Guid tenant,Guid order,Guid preparation,Guid? user,string? role,string? cartCapability,CancellationToken ct)=>(await Send<SnapshotCartClaim>("Cart",HttpMethod.Post,Base(tenant,"cart/claims"),new{OrderId=order,PreparationId=preparation,UserId=user,TenantRole=role},ct,cartCapability:cartCapability))!;
 public Task<SnapshotCartClaim?> CartClaim(Guid tenant,Guid order,CancellationToken ct)=>Send<SnapshotCartClaim>("Cart",HttpMethod.Get,Base(tenant,$"cart/claims/{order}"),null,ct,true);
 public async Task<SnapshotCartClaim> CloseCart(Guid tenant,Guid order,bool complete,string version,CancellationToken ct)=>(await Send<SnapshotCartClaim>("Cart",HttpMethod.Post,Base(tenant,$"cart/claims/{order}/"+(complete?"complete":"release")),new{Version=version},ct))!;
 public async Task<SnapshotProduct[]> Products(Guid tenant,Guid[] variants,CancellationToken ct)=>(await Send<SnapshotProduct[]>("Catalog",HttpMethod.Get,Base(tenant,"catalog/products?")+string.Join("&",variants.Select(id=>"ids="+id)),null,ct))!;
 public async Task<ShippingQuoteSnapshot> ShippingQuote(Guid tenant,Guid quote,CancellationToken ct)=>(await Send<ShippingQuoteSnapshot>("Shipping",HttpMethod.Get,Base(tenant,$"shipping/quotes/{quote}"),null,ct))!;
 public async Task<RemoteRedemption> ClaimPrice(Guid tenant,Guid order,Guid quote,Guid? user,CancellationToken ct)=>(await Send<RemoteRedemption>("Pricing",HttpMethod.Post,Base(tenant,"pricing/redemptions"),new{OrderId=order,QuoteId=quote,UserId=user},ct))!;
 public Task<RemoteRedemption?> Redemption(Guid tenant,Guid order,CancellationToken ct)=>Send<RemoteRedemption>("Pricing",HttpMethod.Get,Base(tenant,$"pricing/redemptions/{order}"),null,ct,true);
 public async Task<RemoteRedemption> ClosePrice(Guid tenant,Guid order,bool redeem,string version,CancellationToken ct)=>(await Send<RemoteRedemption>("Pricing",HttpMethod.Post,Base(tenant,$"pricing/redemptions/{order}/"+(redeem?"redeem":"release")),new{Version=version},ct))!;
 public async Task<RemoteWarehouse[]> Warehouses(Guid tenant,Guid[] variants,DateTimeOffset expiry,CancellationToken ct)=>(await Send<RemoteWarehouse[]>("Inventory",HttpMethod.Get,Base(tenant,"inventory/allocation-options?")+string.Join("&",variants.Select(id=>"variants="+id))+"&expiresAt="+Uri.EscapeDataString(expiry.ToString("O")),null,ct))!;
 public async Task<RemoteReservation> Reserve(Guid tenant,Guid id,Guid warehouse,string reference,DateTimeOffset expiry,OrderQuantity[] items,CancellationToken ct)=>(await Send<RemoteReservation>("Inventory",HttpMethod.Post,Base(tenant,"inventory/reservations"),new{Id=id,WarehouseId=warehouse,Reference=reference,ExpiresAt=expiry,Items=items},ct))!;
 public Task<RemoteReservation?> Reservation(Guid tenant,Guid id,CancellationToken ct)=>Send<RemoteReservation>("Inventory",HttpMethod.Get,Base(tenant,$"inventory/reservations/{id}"),null,ct,true);
 public async Task<RemoteReservation> CloseStock(Guid tenant,Guid id,bool commit,string version,CancellationToken ct)=>(await Send<RemoteReservation>("Inventory",HttpMethod.Post,Base(tenant,$"inventory/reservations/{id}/"+(commit?"commit":"release")),new{Version=version},ct))!;
 public async Task<RemotePayment> StartPayment(Guid tenant,Guid order,CancellationToken ct)=>(await Send<RemotePayment>("Payment",HttpMethod.Post,Base(tenant,"payments"),new{OrderId=order},ct))!;
 public Task<RemotePayment?> Payment(Guid tenant,Guid order,CancellationToken ct)=>Send<RemotePayment>("Payment",HttpMethod.Get,Base(tenant,$"payments/orders/{order}"),null,ct,true);
 public async Task<RemotePayment> CapturePayment(Guid tenant,Guid order,string version,CancellationToken ct)=>(await Send<RemotePayment>("Payment",HttpMethod.Post,Base(tenant,$"payments/orders/{order}/capture"),new{Version=version},ct))!;
 public async Task Refund(Guid tenant,Guid order,Guid operation,decimal amount,string reason,CancellationToken ct)=>_=await Send<object>("Payment",HttpMethod.Post,Base(tenant,$"payments/orders/{order}/refunds"),new{Id=operation,Amount=amount,Reason=reason},ct);
 public async Task CreateShipment(Guid tenant,Guid fulfillment,CancellationToken ct)=>_=await Send<object>("Shipping",HttpMethod.Post,Base(tenant,"shipping/shipments"),new{FulfillmentId=fulfillment},ct);
 public async Task ReceiveReturn(Guid tenant,Guid operation,Guid order,Guid warehouse,OrderQuantity[] items,bool resalable,CancellationToken ct)=>_=await Send<object>("Inventory",HttpMethod.Post,Base(tenant,"inventory/customer-returns"),new{OperationId=operation,OrderId=order,WarehouseId=warehouse,Items=items,Resalable=resalable},ct);
}
