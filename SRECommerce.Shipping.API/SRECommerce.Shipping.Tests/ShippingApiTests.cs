using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Cart.Application;
using SRECommerce.Inventory.Application;
using SRECommerce.Order.Application;
using SRECommerce.Order.Domain;
using SRECommerce.Order.Tests;
using SRECommerce.Shipping.Application;
using SRECommerce.Shipping.Domain;
using SRECommerce.Shipping.Infrastructure;
using Xunit;
using static SRECommerce.Shipping.Tests.ShippingFixture;
namespace SRECommerce.Shipping.Tests;

public sealed class ShippingApiTests(ShippingFixture fixture):IClassFixture<ShippingFixture>
{
 [Fact]public async Task Fully_discounted_cart_and_free_shipping_complete_with_a_zero_charge_receipt()
 {
  using var b=await fixture.Business(flat:0,free:true);Assert.Equal(0,b.Order.Preparation.Quote.Calculation.Total);Assert.Equal(0,b.Option.Total);
  await fixture.Payment.Order.Start(b.Order);var checkout=await fixture.Payment.Order.Drain(b.Order);Assert.Equal(CheckoutState.Accepted,checkout.State);Assert.Equal(0,checkout.Order!.Total);
  using var payment=await fixture.Payment.Machine();var path=SRECommerce.Payment.Tests.PaymentFixture.MachinePath(b.Tenant)+$"/orders/{checkout.Id}";
  var receipt=(await payment.GetFromJsonAsync<SRECommerce.Payment.Application.PaymentView>(path))!;Assert.Equal(0,receipt.Amount);Assert.Equal(SRECommerce.Payment.Domain.PaymentStatus.Captured,receipt.Status);
  Assert.Equal(HttpStatusCode.Conflict,(await payment.PostAsJsonAsync(path+"/refunds",new SRECommerce.Payment.Application.RefundRequest(Guid.NewGuid(),1,"No captured money exists"))).StatusCode);
  using var scope=fixture.Payment.Factory.Services.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Payment");var db=scope.ServiceProvider.GetRequiredService<SRECommerce.Payment.Infrastructure.PaymentDbContext>();Assert.Equal("zero-amount.completed",(await db.Attempts.SingleAsync()).Action);
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task Verified_cart_quotes_drive_the_real_Order_and_Payment_checkout(bool guest)
 {
  using var b=await fixture.Business(guest:guest,flat:10,perUnit:1.5m,tax:18);
  var regions=await b.Customer.GetFromJsonAsync<JsonElement>(Path(b.Tenant,true)+"/regions");Assert.Equal("IN",Assert.Single(regions.GetProperty("countries").EnumerateArray()).GetString());Assert.False(regions.TryGetProperty("warehouses",out _));
  Assert.Equal(13,b.Option.Net);Assert.Equal(2.34m,b.Option.Tax);Assert.Equal(15.34m,b.Option.Total);
  await fixture.Payment.Order.Start(b.Order);var checkout=await fixture.Payment.Order.Drain(b.Order);
  Assert.Equal(CheckoutState.Accepted,checkout.State);Assert.Equal(215.34m,checkout.Order!.Total);Assert.Equal(PaymentState.Captured,checkout.Order.PaymentState);
  using var payment=await fixture.Payment.Machine();var record=await payment.GetFromJsonAsync<SRECommerce.Payment.Application.PaymentView>(SRECommerce.Payment.Tests.PaymentFixture.MachinePath(b.Tenant)+$"/orders/{checkout.Id}");
  Assert.Equal(215.34m,record!.Amount);Assert.Equal(SRECommerce.Payment.Domain.PaymentStatus.Captured,record.Status);
 }
 [Fact]public async Task Quote_eligibility_prices_and_free_threshold_use_authoritative_preparation_values()
 {
  using var b=await fixture.Business();var request=new QuoteOptionsRequest(Guid.NewGuid(),b.Order.Preparation.Id,"IN","110001");var path=Path(b.Tenant)+"/quotes/options";
  var first=await Read<ShippingOption[]>(await b.Customer.PostAsJsonAsync(path,request));var replay=await Read<ShippingOption[]>(await b.Customer.PostAsJsonAsync(path,request));Assert.Equal(first,replay);
  Assert.Empty(await Read<ShippingOption[]>(await b.Customer.PostAsJsonAsync(path,request with{Id=Guid.NewGuid(),PostalCode="560001"})));
  Assert.Equal(HttpStatusCode.BadRequest,(await b.Customer.PostAsJsonAsync(path,new{id=Guid.NewGuid(),preparationId=b.Order.Preparation.Id,countryCode="IN",postalCode="110001",subtotal=999999,shippingTotal=0})).StatusCode);
  var m=b.Method;await Read<MethodView>(await b.Owner.PutAsJsonAsync(Path(b.Tenant)+$"/methods/{m.Id}",new MethodRequest(Guid.NewGuid(),m.ZoneId,m.Code,m.Name,m.Description,m.Kind,m.Currency,m.FlatFee,m.PerUnitFee,m.TaxRate,200,0,null,m.MaximumUnits,m.PickupWarehouseId,true,m.Version)));
  var free=Assert.Single(await Read<ShippingOption[]>(await b.Customer.PostAsJsonAsync(path,request with{Id=Guid.NewGuid()})));Assert.Equal(0,free.Total);
 }
 [Fact]public async Task Guest_quote_requires_cart_proof_and_public_options_do_not_expose_warehouse_identity()
 {
  using var b=await fixture.Business(guest:true,kind:DeliveryKind.StorePickup,flat:0);using var missing=fixture.Factory.Client();using var wrong=fixture.Factory.Client(cartKey:SRECommerce.Cart.Tests.CartFixture.Key());
  var request=new QuoteOptionsRequest(Guid.NewGuid(),b.Order.Preparation.Id,"IN","110001");var path=Path(b.Tenant,true)+"/quotes/options";
  Assert.Equal(HttpStatusCode.Unauthorized,(await missing.PostAsJsonAsync(path,request)).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await wrong.PostAsJsonAsync(path,request)).StatusCode);
  var options=await Read<JsonElement>(await b.Customer.PostAsJsonAsync(path,request));Assert.DoesNotContain("pickupWarehouseId",options.GetRawText(),StringComparison.OrdinalIgnoreCase);
  using var order=fixture.Factory.Client(await fixture.Payment.Order.Token("Order","internal_shipping"));var quote=await order.GetFromJsonAsync<ShippingQuoteContract>(MachinePath(b.Tenant)+$"/quotes/{b.Option.Id}");Assert.Equal(b.Order.Warehouse,quote!.PickupWarehouseId);
 }
 [Fact]public async Task Shipping_read_scopes_cannot_claim_carts_or_reserve_stock()
 {
  using var b=await fixture.Business();var user=b.Order.Preparation.UserId;
  using var cart=fixture.Payment.Order.Cart.Factory.Client(await fixture.Payment.Order.Token("Shipping","internal_cart"));
  await Read<PreparedCartView>(await cart.PostAsJsonAsync($"/internal/v1/tenants/{b.Tenant}/cart/preparations/{b.Order.Preparation.Id}/shipping-verify",new PreparationProof(user,"Owner")));
  Assert.Equal(HttpStatusCode.Forbidden,(await cart.PostAsJsonAsync($"/internal/v1/tenants/{b.Tenant}/cart/claims",new CartClaimRequest(Guid.NewGuid(),b.Order.Preparation.Id,user,"Owner"))).StatusCode);
  using var inventory=fixture.Payment.Order.Cart.Inventory.Client(await fixture.Payment.Order.Token("Shipping","internal_inventory"));
  await Read<WarehouseProof>(await inventory.GetAsync($"/internal/v1/tenants/{b.Tenant}/inventory/warehouses/{b.Order.Warehouse}"));
  Assert.Equal(HttpStatusCode.Forbidden,(await inventory.PostAsJsonAsync($"/internal/v1/tenants/{b.Tenant}/inventory/reservations",new ReserveRequest(Guid.NewGuid(),b.Order.Warehouse,"Not permitted",fixture.Payment.Order.Cart.Inventory.Clock.GetUtcNow().AddMinutes(5),[new(b.Order.Variant,1)]))).StatusCode);
 }
 [Fact]public async Task Quote_cannot_be_reused_for_a_different_preparation_with_identical_items()
 {
  using var b=await fixture.Business();var prepared=await Read<PreparedCartView>(await b.Order.CartClient.PostAsJsonAsync(SRECommerce.Cart.Tests.CartFixture.Path(b.Tenant)+$"/{b.Order.Cart.Id}/prepare",new PrepareCartRequest(Guid.NewGuid(),b.Order.Cart.Version)));
  var mismatched=b.Order with{Request=b.Order.Request with{PreparationId=prepared.Id}};await fixture.Payment.Order.Start(mismatched);var checkout=await fixture.Payment.Order.Drain(mismatched);
  Assert.Equal(CheckoutState.Failed,checkout.State);Assert.Null(checkout.Order);
 }
 [Fact]public async Task Partial_shipment_delivery_updates_Order_only_through_durable_scoped_callbacks()
 {
  using var b=await fixture.Business();var shipment=await fixture.Shipment(b,1);shipment=await fixture.Move(b,shipment,ShipmentStatus.Packed);shipment=await fixture.Move(b,shipment,ShipmentStatus.Dispatched);
  await fixture.Callbacks(b.Tenant);var order=(await b.Order.Client.GetFromJsonAsync<OrderView>(OrderFixture.Path(b.Tenant)+$"/{b.Order.Request.Id}"))!;Assert.Equal(OrderStatus.PartiallyFulfilled,order.Status);Assert.Equal(1,Assert.Single(order.Items).Fulfilled);
  shipment=await fixture.Move(b,shipment,ShipmentStatus.Delivered);await fixture.Callbacks(b.Tenant);order=(await b.Order.Client.GetFromJsonAsync<OrderView>(OrderFixture.Path(b.Tenant)+$"/{b.Order.Request.Id}"))!;Assert.Equal(1,Assert.Single(order.Items).Delivered);
  var tracking=await b.Customer.GetFromJsonAsync<JsonElement>(Path(b.Tenant)+$"/orders/{order.Id}/tracking");Assert.Equal(1,tracking.GetArrayLength());Assert.Equal(4,tracking[0].GetProperty("events").GetArrayLength());
  var other=await fixture.Payment.Order.Cart.Pricing.Customer(b.Order.Pricing);using var pricing=other.Client;using var stranger=fixture.Factory.Client(pricing.DefaultRequestHeaders.Authorization!.Parameter);
  Assert.Equal(HttpStatusCode.NotFound,(await stranger.GetAsync(Path(b.Tenant)+$"/orders/{order.Id}/tracking")).StatusCode);
 }
 [Fact]public async Task Lost_callback_response_replays_the_persisted_request_without_double_fulfillment()
 {
  using var b=await fixture.Business();var shipment=await fixture.Shipment(b);shipment=await fixture.Move(b,shipment,ShipmentStatus.Packed);shipment=await fixture.Move(b,shipment,ShipmentStatus.Dispatched);
  var count=fixture.Factory.UpdateRequests;
  try
  {
   fixture.Factory.LoseUpdateResponseOnce=true;await fixture.Callbacks(b.Tenant);Assert.Equal(count+1,fixture.Factory.UpdateRequests);
   fixture.Payment.Order.Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromSeconds(3));await fixture.Callbacks(b.Tenant);Assert.Equal(count+2,fixture.Factory.UpdateRequests);
   var order=(await b.Order.Client.GetFromJsonAsync<OrderView>(OrderFixture.Path(b.Tenant)+$"/{b.Order.Request.Id}"))!;Assert.Equal(2,Assert.Single(order.Items).Fulfilled);
   var history=await b.Owner.GetFromJsonAsync<JsonElement>(Path(b.Tenant)+$"/shipments/{shipment.Id}/history");Assert.True(Assert.Single(history.GetProperty("callbacks").EnumerateArray()).GetProperty("sent").GetBoolean());
  }
  finally{fixture.Factory.LoseUpdateResponseOnce=false;}
 }
 [Fact]public async Task Shipment_state_survives_Order_outage_and_delivers_queued_updates_in_sequence()
 {
  using var b=await fixture.Business();var shipment=await fixture.Shipment(b);shipment=await fixture.Move(b,shipment,ShipmentStatus.Packed);shipment=await fixture.Move(b,shipment,ShipmentStatus.Dispatched);shipment=await fixture.Move(b,shipment,ShipmentStatus.Delivered);
  try{fixture.Factory.OrderOffline=true;await fixture.Callbacks(b.Tenant);}finally{fixture.Factory.OrderOffline=false;}
  fixture.Payment.Order.Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromSeconds(3));await fixture.Callbacks(b.Tenant);await fixture.Callbacks(b.Tenant);
  var order=(await b.Order.Client.GetFromJsonAsync<OrderView>(OrderFixture.Path(b.Tenant)+$"/{b.Order.Request.Id}"))!;Assert.Equal(OrderStatus.Delivered,order.Status);Assert.Equal(2,Assert.Single(order.Items).Delivered);
 }
 [Fact]public async Task Guest_tracking_requires_Order_capability_and_COD_settles_after_actual_delivery_callback()
 {
  using var b=await fixture.Business(guest:true,paymentMethod:"COD");var shipment=await fixture.Shipment(b);shipment=await fixture.Move(b,shipment,ShipmentStatus.Packed);shipment=await fixture.Move(b,shipment,ShipmentStatus.Dispatched);shipment=await fixture.Move(b,shipment,ShipmentStatus.Delivered);
  await fixture.Callbacks(b.Tenant);await fixture.Callbacks(b.Tenant);await fixture.Payment.Order.Work(b.Tenant);
  var path=Path(b.Tenant,true)+$"/orders/{b.Order.Request.Id}/tracking";await Read<JsonElement>(await b.Customer.GetAsync(path));using var missing=fixture.Factory.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await missing.GetAsync(path)).StatusCode);
  var order=(await b.Order.Client.GetFromJsonAsync<OrderView>(OrderFixture.Path(b.Tenant,true)+$"/{b.Order.Request.Id}"))!;Assert.Equal(PaymentState.Captured,order.PaymentState);Assert.Equal(OrderStatus.Delivered,order.Status);
 }
 [Fact]public async Task Return_pickup_is_reschedulable_after_cancellation_and_does_not_receive_stock_or_refund()
 {
  using var b=await fixture.Business();var shipment=await fixture.Shipment(b);shipment=await fixture.Move(b,shipment,ShipmentStatus.Packed);shipment=await fixture.Move(b,shipment,ShipmentStatus.Dispatched);shipment=await fixture.Move(b,shipment,ShipmentStatus.Delivered);await fixture.Callbacks(b.Tenant);await fixture.Callbacks(b.Tenant);
  var path=OrderFixture.Path(b.Tenant)+$"/{b.Order.Request.Id}";var order=(await b.Order.Client.GetFromJsonAsync<OrderView>(path))!;
  var returned=await Read<ReturnView>(await b.Order.Client.PostAsJsonAsync(path+"/returns",new ReturnOrderRequest(Guid.NewGuid(),order.Version,[new(b.Order.Variant,1)],"Customer return")));
  returned=await Read<ReturnView>(await b.Order.Owner.PostAsJsonAsync(OrderFixture.Manage(b.Tenant)+$"/returns/{returned.Id}/status",new ReturnStateRequest(Guid.NewGuid(),returned.Version,ReturnStatus.Approved)));
  var now=fixture.Payment.Order.Cart.Pricing.Factory.Clock.GetUtcNow();var request=new ReturnPickupRequest(Guid.NewGuid(),returned.Id,b.Order.Warehouse,now.AddHours(1),now.AddHours(2));var pickup=await Read<ReturnPickupView>(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+"/return-pickups",request));
  Assert.Equal(HttpStatusCode.Conflict,(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+"/return-pickups",request with{Id=Guid.NewGuid()})).StatusCode);
  await Read<ReturnPickupView>(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+$"/return-pickups/{pickup.Id}/status",new ReturnPickupStateRequest(Guid.NewGuid(),pickup.Version,PickupStatus.Cancelled,"Reschedule requested")));
  pickup=await Read<ReturnPickupView>(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+"/return-pickups",request with{Id=Guid.NewGuid()}));
  await Read<ReturnPickupView>(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+$"/return-pickups/{pickup.Id}/status",new ReturnPickupStateRequest(Guid.NewGuid(),pickup.Version,PickupStatus.Collected,"Collected for warehouse quality inspection")));
  order=(await b.Order.Client.GetFromJsonAsync<OrderView>(path))!;Assert.Equal(0,order.Refunded);Assert.Equal(0,Assert.Single(order.Items).Returned);Assert.Equal(ReturnStatus.Approved,Assert.Single((await b.Order.Client.GetFromJsonAsync<ReturnView[]>(path+"/returns"))!).Status);
 }
 [Fact]public async Task Expired_quotes_and_dependency_outages_fail_closed_without_losing_the_saved_cart()
 {
  using var b=await fixture.Business();
  try{fixture.Factory.CartOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await b.Customer.PostAsJsonAsync(Path(b.Tenant)+"/quotes/options",new QuoteOptionsRequest(Guid.NewGuid(),b.Order.Preparation.Id,"IN","110001"))).StatusCode);}finally{fixture.Factory.CartOffline=false;}
  Assert.Equal(HttpStatusCode.OK,(await b.Order.CartClient.GetAsync(SRECommerce.Cart.Tests.CartFixture.Path(b.Tenant)+$"/{b.Order.Cart.Id}")).StatusCode);
  fixture.Payment.Order.Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromMinutes(6));using var order=fixture.Factory.Client(await fixture.Payment.Order.Token("Order","internal_shipping"));Assert.Equal(HttpStatusCode.Conflict,(await order.GetAsync(MachinePath(b.Tenant)+$"/quotes/{b.Option.Id}")).StatusCode);
 }
 [Fact]public async Task Shipment_and_quote_snapshots_are_immutable_and_all_queries_require_tenant_scope()
 {
  using var b=await fixture.Business();var shipment=await fixture.Shipment(b);using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShippingDbContext>();Assert.Empty(await db.Shipments.ToListAsync());scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Shipping");
  var row=await db.Shipments.SingleAsync();row.ContactDocument="{}";Assert.Equal("ShipmentSnapshotImmutable",(await Assert.ThrowsAsync<BusinessFault>(()=>db.SaveChangesAsync())).Code);db.ChangeTracker.Clear();
  var quote=await db.Quotes.SingleAsync();quote.Document="{}";Assert.Equal("ShippingHistoryImmutable",(await Assert.ThrowsAsync<BusinessFault>(()=>db.SaveChangesAsync())).Code);
 }
}
