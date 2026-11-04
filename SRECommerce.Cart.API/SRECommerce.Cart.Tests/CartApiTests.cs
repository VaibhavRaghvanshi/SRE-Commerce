using System.Net;using System.Net.Http.Json;using System.Text.Json;using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Application;using SRECommerce.Cart.Domain;using SRECommerce.Cart.Infrastructure;using Xunit;
using static SRECommerce.Cart.Tests.CartFixture;
namespace SRECommerce.Cart.Tests;
public sealed class CartApiTests(CartFixture fixture):IClassFixture<CartFixture>
{
 [Fact]public async Task Customer_addresses_wishlist_and_recent_history_are_private_versioned_and_catalog_revalidated()
 {
  var b=await fixture.Business();using var pricingOwner=b.Client;using var catalog=b.Catalog.Client;
  var a=await fixture.Pricing.Customer(b);var other=await fixture.Pricing.Customer(b);using var customerPrice=a.Client;using var otherPrice=other.Client;
  using var customer=fixture.Factory.Client(a.Client.DefaultRequestHeaders.Authorization!.Parameter);using var stranger=fixture.Factory.Client(other.Client.DefaultRequestHeaders.Authorization!.Parameter);
  using var anonymous=fixture.Factory.Client();var path=$"/api/v1/tenants/{b.Catalog.Id}/cart/account";var addressId=Guid.NewGuid();
  var input=new AddressInput("Home","Account customer","customer@example.test",null,"1 Test Street",null,"Delhi","IN","110001",null);
  var address=await Read<AddressView>(await customer.PutAsJsonAsync(path+"/addresses/"+addressId,input));
  Assert.Equal(addressId,address.Id);Assert.Empty((await stranger.GetFromJsonAsync<AddressView[]>(path+"/addresses"))!);
  Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path+"/addresses")).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await stranger.PostAsJsonAsync(path+$"/addresses/{addressId}/archive",new AddressVersion(address.Version))).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await customer.PutAsJsonAsync(path+"/addresses/"+addressId,input with{Label="Work",Version=Convert.ToBase64String(new byte[8])})).StatusCode);
  address=await Read<AddressView>(await customer.PutAsJsonAsync(path+"/addresses/"+addressId,input with{Label="Work",Version=address.Version}));
  Assert.Equal("Work",address.Label);
  var saved=await Read<SavedProductView>(await customer.PutAsJsonAsync(path+"/products/0/"+b.Product,new SavedProductInput(true,null)));
  Assert.Equal(b.Product,saved.ProductId);Assert.True(saved.Available);
  Assert.Single((await customer.GetFromJsonAsync<SavedProductPage>(path+"/products/0"))!.Items);
  Assert.Empty((await stranger.GetFromJsonAsync<SavedProductPage>(path+"/products/0"))!.Items);
  Assert.Equal(HttpStatusCode.Conflict,(await customer.PutAsJsonAsync(path+"/products/0/"+b.Product,new SavedProductInput(false,Convert.ToBase64String(new byte[8])))).StatusCode);
  await Read<SavedProductView>(await customer.PutAsJsonAsync(path+"/products/1/"+b.Product,new SavedProductInput(true,null)));
  Assert.Single((await customer.GetFromJsonAsync<SavedProductPage>(path+"/products/1"))!.Items);
  Assert.Equal(HttpStatusCode.NoContent,(await customer.PostAsync(path+"/recently-viewed/clear",null)).StatusCode);
  Assert.Empty((await customer.GetFromJsonAsync<SavedProductPage>(path+"/products/1"))!.Items);
  var product=(await catalog.GetFromJsonAsync<SRECommerce.Catalog.Application.ProductView>($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{b.Product}"))!;
  Assert.Equal(HttpStatusCode.NoContent,(await catalog.PostAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{b.Product}/archive",new SRECommerce.Catalog.Application.VersionRequest(product.Version!))).StatusCode);
  var unavailable=Assert.Single((await customer.GetFromJsonAsync<SavedProductPage>(path+"/products/0"))!.Items);Assert.False(unavailable.Available);Assert.Null(unavailable.Slug);
  await Read<SavedProductView>(await customer.PutAsJsonAsync(path+"/products/0/"+b.Product,new SavedProductInput(false,unavailable.Version)));
  Assert.Empty((await customer.GetFromJsonAsync<SavedProductPage>(path+"/products/0"))!.Items);
  Assert.Equal(HttpStatusCode.NoContent,(await customer.PostAsJsonAsync(path+$"/addresses/{addressId}/archive",new AddressVersion(address.Version))).StatusCode);
  Assert.Empty((await customer.GetFromJsonAsync<AddressView[]>(path+"/addresses"))!);
 }
 [Fact]public async Task Cancellation_fences_block_delayed_cart_price_and_stock_claims_without_mutating_an_unproved_cart()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);
  var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var path=Path(b.Catalog.Id,true)+$"/{cart.Id}";
  var prepared=await Read<PreparedCartView>(await guest.PostAsJsonAsync(path+"/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  var orderId=Guid.NewGuid();using var cartOrder=fixture.Factory.Client(await fixture.Machine("Order"),key);var cartClaims=$"/internal/v1/tenants/{b.Catalog.Id}/cart/claims";var claim=new CartClaimRequest(orderId,prepared.Id,null);
  await Read<JsonElement>(await cartOrder.PostAsJsonAsync(cartClaims+"/cancel",claim));
  Assert.Equal(CartClaimStatus.Released,(await Read<CartClaimView>(await cartOrder.PostAsJsonAsync(cartClaims,claim))).Status);
  cart=await Set(guest,b.Catalog.Id,cart,b.Variant,2,true);Assert.Equal(2,Assert.Single(cart.Items).Quantity);

  using var priceOrder=fixture.Pricing.Factory.Client(await fixture.Machine("Order","internal_pricing"));var redemptions=$"/internal/v1/tenants/{b.Catalog.Id}/pricing/redemptions";
  var priceClaim=new SRECommerce.Pricing.Application.RedemptionRequest(orderId,prepared.Quote.Id,null);
  Assert.Equal(SRECommerce.Pricing.Domain.RedemptionStatus.Released,(await Read<SRECommerce.Pricing.Application.RedemptionView>(await priceOrder.PostAsJsonAsync(redemptions+"/cancel",priceClaim))).Status);
  Assert.Equal(SRECommerce.Pricing.Domain.RedemptionStatus.Released,(await Read<SRECommerce.Pricing.Application.RedemptionView>(await priceOrder.PostAsJsonAsync(redemptions,priceClaim))).Status);

  using var inventoryOwner=fixture.Inventory.Client(b.Catalog.Token);var stockPath=$"/api/v1/tenants/{b.Catalog.Id}/inventory";var lookups=await inventoryOwner.GetFromJsonAsync<JsonElement>(stockPath+"/lookups");var warehouse=lookups.GetProperty("warehouses")[0].GetProperty("id").GetGuid();
  using var stockOrder=fixture.Inventory.Client(await fixture.Machine("Order","internal_inventory"));var reservations=$"/internal/v1/tenants/{b.Catalog.Id}/inventory/reservations";var reservationId=Guid.NewGuid();var cancel=new SRECommerce.Inventory.Application.ReservationAbortRequest(Guid.NewGuid(),"Checkout cancelled before delayed reserve");
  await Read<JsonElement>(await stockOrder.PostAsJsonAsync(reservations+$"/{reservationId}/cancel",cancel));
  Assert.Equal(HttpStatusCode.Conflict,(await stockOrder.PostAsJsonAsync(reservations,new SRECommerce.Inventory.Application.ReserveRequest(reservationId,warehouse,"Delayed order",fixture.Inventory.Clock.GetUtcNow().AddMinutes(5),[new(b.Variant,1)]))).StatusCode);
  using var other=fixture.Inventory.Client(await fixture.Machine("Manufacturing","internal_inventory"));
  Assert.Equal(HttpStatusCode.BadRequest,(await other.PostAsJsonAsync(reservations+$"/{reservationId}/cancel",cancel)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync(reservations+$"/{reservationId}/cancel",cancel with{OperationId=reservationId})).StatusCode);
  using var publicStock=fixture.Inventory.Client();var available=(await publicStock.GetFromJsonAsync<SRECommerce.Inventory.Application.AvailabilityView[]>($"/api/v1/public/tenants/{b.Catalog.Id}/inventory/availability?variants={b.Variant}"))!;Assert.Equal(10,Assert.Single(available).Available);
 }
 private static async Task<CartView> Create(HttpClient client,Guid tenant,bool guest=false)=>await Read<CartView>(await client.PostAsJsonAsync(Path(tenant,guest),new CreateCartRequest(Guid.NewGuid())));
 private static async Task<CartView> Set(HttpClient client,Guid tenant,CartView cart,Guid variant,decimal quantity,bool guest=false)=>await Read<CartView>(await client.PutAsJsonAsync(Path(tenant,guest)+$"/{cart.Id}/items",new CartItemRequest(Guid.NewGuid(),variant,quantity,cart.Version)));



 [Fact]public async Task Allocation_options_are_machine_only_respect_expiry_horizon_and_support_recovery_after_disablement()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var stockPath=$"/api/v1/tenants/{b.Catalog.Id}/inventory";using var inventoryOwner=fixture.Inventory.Client(b.Catalog.Token);var metadata=await inventoryOwner.GetFromJsonAsync<JsonElement>(stockPath+"/lookups");var warehouse=metadata.GetProperty("warehouses")[0].GetProperty("id").GetGuid();
  await Read<JsonElement>(await inventoryOwner.PostAsJsonAsync(stockPath+"/stock-changes",new SRECommerce.Inventory.Application.StockChangeRequest(Guid.NewGuid(),warehouse,b.Variant,SRECommerce.Inventory.Domain.StockAction.Receipt,7,"Short lived lot",Lot:"SHORT",ExpiresAt:fixture.Inventory.Clock.GetUtcNow().AddMinutes(3))));
  var machine=$"/internal/v1/tenants/{b.Catalog.Id}/inventory";var expires=fixture.Inventory.Clock.GetUtcNow().AddMinutes(10);var options=machine+"/allocation-options?variants="+b.Variant+"&expiresAt="+Uri.EscapeDataString(expires.ToString("O"));
  Assert.Equal(HttpStatusCode.Unauthorized,(await inventoryOwner.GetAsync(options)).StatusCode);using var order=fixture.Inventory.Client(await fixture.Machine("Order","internal_inventory"));var warehouses=(await order.GetFromJsonAsync<SRECommerce.Inventory.Application.WarehouseAvailability[]>(options))!;Assert.Equal(10,Assert.Single(Assert.Single(warehouses).Items).Available);
  using var catalogOrder=fixture.Pricing.Catalog.Factory.Client(await fixture.Machine("Order","internal_catalog"));var sellable=(await catalogOrder.GetFromJsonAsync<SRECommerce.Catalog.Application.ProductView[]>($"/internal/v1/tenants/{b.Catalog.Id}/catalog/products?ids={b.Variant}"))!;Assert.Equal(b.Product,Assert.Single(sellable).Id);
  var hold=new SRECommerce.Inventory.Application.ReserveRequest(Guid.NewGuid(),warehouse,"Order recovery test",expires,[new(b.Variant,2)]);var reservation=await Read<SRECommerce.Inventory.Application.ReservationView>(await order.PostAsJsonAsync(machine+"/reservations",hold));
  using var otherService=fixture.Inventory.Client(await fixture.Machine("Manufacturing","internal_inventory"));Assert.Equal(HttpStatusCode.Forbidden,(await otherService.GetAsync(machine+$"/reservations/{reservation.Id}")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await otherService.PostAsJsonAsync(machine+$"/reservations/{reservation.Id}/release",new SRECommerce.Inventory.Application.CloseReservationRequest(reservation.Version))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await inventoryOwner.PostAsJsonAsync(stockPath+$"/reservations/{reservation.Id}/release",new SRECommerce.Inventory.Application.CloseReservationRequest(reservation.Version))).StatusCode);

  using var tenant=fixture.Pricing.Catalog.Tenant.Factory.Client(b.Catalog.Token);var state=(await tenant.GetFromJsonAsync<SRECommerce.Tenant.Application.TenantView>($"/api/v1/tenants/{b.Catalog.Id}"))!;await Read<SRECommerce.Tenant.Application.TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/modules/inventory",new SRECommerce.Tenant.Application.ModuleRequest(state.Version,false)));
  Assert.Equal(HttpStatusCode.Forbidden,(await order.GetAsync(options)).StatusCode);Assert.Equal(reservation.Id,(await order.GetFromJsonAsync<SRECommerce.Inventory.Application.ReservationView>(machine+$"/reservations/{reservation.Id}"))!.Id);
  Assert.Equal(SRECommerce.Inventory.Domain.ReservationStatus.Released,(await Read<SRECommerce.Inventory.Application.ReservationView>(await order.PostAsJsonAsync(machine+$"/reservations/{reservation.Id}/release",new SRECommerce.Inventory.Application.CloseReservationRequest(reservation.Version)))).Status);
 }
 [Fact]public async Task Pricing_capacity_can_be_inspected_and_released_after_module_disablement()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var anonymous=fixture.Pricing.Factory.Client();var quote=await Read<SRECommerce.Pricing.Application.QuoteView>(await anonymous.PostAsJsonAsync($"/api/v1/public/tenants/{b.Catalog.Id}/pricing/quotes",new SRECommerce.Pricing.Application.QuoteRequest(Guid.NewGuid(),[new(b.Variant,1)])));
  using var order=fixture.Pricing.Factory.Client(await fixture.Machine("Order","internal_pricing"));var machine=$"/internal/v1/tenants/{b.Catalog.Id}/pricing/redemptions";var hold=await Read<SRECommerce.Pricing.Application.RedemptionView>(await order.PostAsJsonAsync(machine,new SRECommerce.Pricing.Application.RedemptionRequest(Guid.NewGuid(),quote.Id,null)));
  using var tenant=fixture.Pricing.Catalog.Tenant.Factory.Client(b.Catalog.Token);var state=(await tenant.GetFromJsonAsync<SRECommerce.Tenant.Application.TenantView>($"/api/v1/tenants/{b.Catalog.Id}"))!;await Read<SRECommerce.Tenant.Application.TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/modules/pricing",new SRECommerce.Tenant.Application.ModuleRequest(state.Version,false)));
  Assert.Equal(hold.OrderId,(await order.GetFromJsonAsync<SRECommerce.Pricing.Application.RedemptionView>(machine+$"/{hold.OrderId}"))!.OrderId);
  Assert.Equal(SRECommerce.Pricing.Domain.RedemptionStatus.Released,(await Read<SRECommerce.Pricing.Application.RedemptionView>(await order.PostAsJsonAsync(machine+$"/{hold.OrderId}/release",new SRECommerce.Pricing.Application.RedemptionVersionRequest(hold.Version)))).Status);
 }
 [Fact]public async Task Checkout_claims_freeze_a_cart_revision_prevent_duplicate_orders_and_release_safely()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var path=Path(b.Catalog.Id,true)+$"/{cart.Id}";
  var first=await Read<PreparedCartView>(await guest.PostAsJsonAsync(path+"/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));var second=await Read<PreparedCartView>(await guest.PostAsJsonAsync(path+"/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var order=fixture.Factory.Client(await fixture.Machine("Order"),key);var machine=$"/internal/v1/tenants/{b.Catalog.Id}/cart/claims";var request=new CartClaimRequest(Guid.NewGuid(),first.Id,null);var claim=await Read<CartClaimView>(await order.PostAsJsonAsync(machine,request));Assert.Equal(CartClaimStatus.Held,claim.Status);
  Assert.Equal(claim.Version,(await Read<CartClaimView>(await order.PostAsJsonAsync(machine,request))).Version);Assert.Equal(HttpStatusCode.Conflict,(await order.PostAsJsonAsync(machine,new CartClaimRequest(Guid.NewGuid(),second.Id,null))).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await guest.PutAsJsonAsync(path+"/items",new CartItemRequest(Guid.NewGuid(),b.Variant,2,cart.Version))).StatusCode);
  order.DefaultRequestHeaders.Remove("X-Cart-Key");Assert.Equal(claim.OrderId,(await order.GetFromJsonAsync<CartClaimView>(machine+$"/{claim.OrderId}"))!.OrderId);
  var released=await Read<CartClaimView>(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/release",new CartClaimVersion(claim.Version)));Assert.Equal(CartClaimStatus.Released,released.Status);Assert.Equal(released.Version,(await Read<CartClaimView>(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/release",new CartClaimVersion(claim.Version)))).Version);
  cart=(await guest.GetFromJsonAsync<CartView>(path))!;cart=await Set(guest,b.Catalog.Id,cart,b.Variant,2,true);Assert.Equal(2,Assert.Single(cart.Items).Quantity);
 }
 [Fact]public async Task Claim_completion_is_idempotent_closes_the_cart_and_checks_the_prepared_customer_role()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var customer=fixture.Factory.Client(b.Catalog.Token);var cart=await Set(customer,b.Catalog.Id,await Create(customer,b.Catalog.Id),b.Variant,1);var path=Path(b.Catalog.Id)+$"/{cart.Id}";var prepared=await Read<PreparedCartView>(await customer.PostAsJsonAsync(path+"/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var order=fixture.Factory.Client(await fixture.Machine("Order"));var machine=$"/internal/v1/tenants/{b.Catalog.Id}/cart/claims";
  Assert.Equal(HttpStatusCode.Conflict,(await order.PostAsJsonAsync(machine,new CartClaimRequest(Guid.NewGuid(),prepared.Id,prepared.UserId,"Customer"))).StatusCode);
  var claim=await Read<CartClaimView>(await order.PostAsJsonAsync(machine,new CartClaimRequest(Guid.NewGuid(),prepared.Id,prepared.UserId,"Owner")));var completed=await Read<CartClaimView>(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/complete",new CartClaimVersion(claim.Version)));Assert.Equal(CartClaimStatus.Completed,completed.Status);
  Assert.Equal(completed.Version,(await Read<CartClaimView>(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/complete",new CartClaimVersion(claim.Version)))).Version);Assert.Equal(HttpStatusCode.Conflict,(await customer.GetAsync(path)).StatusCode);Assert.NotEqual(cart.Id,(await Create(customer,b.Catalog.Id)).Id);
 }
 [Fact]public async Task Existing_checkout_claims_can_be_released_after_module_disablement()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var prepared=await Read<PreparedCartView>(await guest.PostAsJsonAsync(Path(b.Catalog.Id,true)+$"/{cart.Id}/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var order=fixture.Factory.Client(await fixture.Machine("Order"),key);var machine=$"/internal/v1/tenants/{b.Catalog.Id}/cart/claims";var claim=await Read<CartClaimView>(await order.PostAsJsonAsync(machine,new CartClaimRequest(Guid.NewGuid(),prepared.Id,null)));
  using var tenant=fixture.Pricing.Catalog.Tenant.Factory.Client(b.Catalog.Token);var value=(await tenant.GetFromJsonAsync<SRECommerce.Tenant.Application.TenantView>($"/api/v1/tenants/{b.Catalog.Id}"))!;await Read<SRECommerce.Tenant.Application.TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/modules/cart",new SRECommerce.Tenant.Application.ModuleRequest(value.Version,false)));
  Assert.Equal(HttpStatusCode.Forbidden,(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/complete",new CartClaimVersion(claim.Version))).StatusCode);Assert.Equal(CartClaimStatus.Released,(await Read<CartClaimView>(await order.PostAsJsonAsync(machine+$"/{claim.OrderId}/release",new CartClaimVersion(claim.Version)))).Status);
 }
 [Fact]public async Task Expired_checkout_claims_stop_blocking_guest_mutations()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var prepared=await Read<PreparedCartView>(await guest.PostAsJsonAsync(Path(b.Catalog.Id,true)+$"/{cart.Id}/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var order=fixture.Factory.Client(await fixture.Machine("Order"),key);var machine=$"/internal/v1/tenants/{b.Catalog.Id}/cart/claims";var claim=await Read<CartClaimView>(await order.PostAsJsonAsync(machine,new CartClaimRequest(Guid.NewGuid(),prepared.Id,null)));fixture.Pricing.Factory.Clock.Advance(TimeSpan.FromMinutes(16));Assert.Equal(CartClaimStatus.Expired,(await order.GetFromJsonAsync<CartClaimView>(machine+$"/{claim.OrderId}"))!.Status);
  cart=await Set(guest,b.Catalog.Id,cart,b.Variant,2,true);Assert.Equal(2,Assert.Single(cart.Items).Quantity);
 }
 [Fact]public async Task B2B_items_require_current_membership_and_are_hidden_from_guest_catalog_and_stock()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var cp=$"/api/v1/tenants/{b.Catalog.Id}/catalog";
  var p=(await catalog.GetFromJsonAsync<SRECommerce.Catalog.Application.ProductView>(cp+$"/products/{b.Product}"))!;
  var draft=new SRECommerce.Catalog.Application.ProductDraft(p.Name,p.Slug,p.CategoryId,p.BrandId,p.Kind,p.Description,p.SeoTitle,p.SeoDescription,p.Visible,true,p.Attributes,p.Specifications,p.Variants,p.Media,p.BundleItems,p.Relationships,p.Version);
  p=await Read<SRECommerce.Catalog.Application.ProductView>(await catalog.PutAsJsonAsync(cp+$"/products/{p.Id}",draft));await Read<SRECommerce.Catalog.Application.ProductView>(await catalog.PostAsJsonAsync(cp+$"/products/{p.Id}/publish",new SRECommerce.Catalog.Application.VersionRequest(p.Version!)));
  using var guest=fixture.Factory.Client(capability:Key());var gc=await Create(guest,b.Catalog.Id,true);Assert.Equal(HttpStatusCode.Conflict,(await guest.PutAsJsonAsync(Path(b.Catalog.Id,true)+$"/{gc.Id}/items",new CartItemRequest(Guid.NewGuid(),b.Variant,1,gc.Version))).StatusCode);
  var ordinary=await fixture.Pricing.Customer(b);using var ordinaryPricing=ordinary.Client;using var customer=fixture.Factory.Client(ordinary.Client.DefaultRequestHeaders.Authorization!.Parameter);var cc=await Create(customer,b.Catalog.Id);Assert.Equal(HttpStatusCode.Conflict,(await customer.PutAsJsonAsync(Path(b.Catalog.Id)+$"/{cc.Id}/items",new CartItemRequest(Guid.NewGuid(),b.Variant,1,cc.Version))).StatusCode);
  var buyer=await fixture.Pricing.Customer(b,"B2BBuyer");using var buyerPricing=buyer.Client;using var buyerCart=fixture.Factory.Client(buyer.Client.DefaultRequestHeaders.Authorization!.Parameter);var bc=await Set(buyerCart,b.Catalog.Id,await Create(buyerCart,b.Catalog.Id),b.Variant,1);
  var preview=await Read<CartPreview>(await buyerCart.PostAsJsonAsync(Path(b.Catalog.Id)+$"/{bc.Id}/preview",new CartPreviewRequest(Guid.NewGuid())));Assert.True(preview.CanPrepare);Assert.Equal(100,preview.Quote.Calculation.Total);
  using var identity=fixture.Pricing.Catalog.Tenant.Identity.Factory.Client();identity.DefaultRequestHeaders.Authorization=new("Bearer",b.Catalog.Token);Assert.Equal(HttpStatusCode.NoContent,(await identity.PutAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/memberships/{buyer.Id}",new SRECommerce.Identity.Application.MembershipRequest(buyer.Id,"Customer"))).StatusCode);
  // Identity revokes existing sessions when a membership role changes.
  Assert.Equal(HttpStatusCode.Unauthorized,(await buyerCart.PostAsJsonAsync(Path(b.Catalog.Id)+$"/{bc.Id}/preview",new CartPreviewRequest(Guid.NewGuid()))).StatusCode);
 }
 [Fact]public async Task Preparation_and_operation_history_cannot_be_modified_or_read_without_tenant_scope()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var client=fixture.Factory.Client(b.Catalog.Token);var cart=await Set(client,b.Catalog.Id,await Create(client,b.Catalog.Id),b.Variant,1);var prepared=await Read<PreparedCartView>(await client.PostAsJsonAsync(Path(b.Catalog.Id)+$"/{cart.Id}/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CartDbContext>();Assert.Empty(await db.Preparations.ToListAsync());scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Catalog.Id,"Cart");var row=await db.Preparations.SingleAsync(p=>p.Id==prepared.Id);row.Document="{}";await Assert.ThrowsAsync<BusinessFault>(()=>db.SaveChangesAsync());
 }
 [Fact]public async Task Guest_creation_is_replay_safe_and_capability_is_required_for_every_read()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var missing=fixture.Factory.Client();var path=Path(b.Catalog.Id,true);var request=new CreateCartRequest(Guid.NewGuid());Assert.Equal(HttpStatusCode.Unauthorized,(await missing.PostAsJsonAsync(path,request)).StatusCode);
  var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Read<CartView>(await guest.PostAsJsonAsync(path,request));Assert.Equal(cart.Id,(await Read<CartView>(await guest.PostAsJsonAsync(path,request))).Id);
  using var attacker=fixture.Factory.Client(capability:Key());Assert.Equal(HttpStatusCode.NotFound,(await attacker.GetAsync(path+$"/{cart.Id}")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await missing.GetAsync(path+$"/{cart.Id}")).StatusCode);
  using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CartDbContext>();var session=await db.Sessions.IgnoreQueryFilters().SingleAsync(s=>s.CartId==cart.Id);Assert.Equal(CartRules.Capability(key),session.CapabilityHash);Assert.DoesNotContain(key,JsonSerializer.Serialize(session));
 }
 [Fact]public async Task Item_writes_have_versions_stable_receipts_and_reject_client_prices()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var guest=fixture.Factory.Client(capability:Key());var tenant=b.Catalog.Id;var cart=await Create(guest,tenant,true);var path=Path(tenant,true)+$"/{cart.Id}";var request=new CartItemRequest(Guid.NewGuid(),b.Variant,2,cart.Version);
  var saved=await Read<CartView>(await guest.PutAsJsonAsync(path+"/items",request));Assert.Equal(2,Assert.Single(saved.Items).Quantity);Assert.Equal(saved.Version,(await Read<CartView>(await guest.PutAsJsonAsync(path+"/items",request))).Version);
  Assert.Equal(HttpStatusCode.Conflict,(await guest.PutAsJsonAsync(path+"/items",request with{Quantity=3})).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await guest.PutAsJsonAsync(path+"/items",request with{OperationId=Guid.NewGuid()})).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await guest.PutAsJsonAsync(path+"/items",new{operationId=Guid.NewGuid(),variantId=b.Variant,quantity=1,version=saved.Version,price=0,userId=Guid.NewGuid()})).StatusCode);
  var remove=new RemoveCartItemRequest(Guid.NewGuid(),saved.Version);Assert.Empty((await Read<CartView>(await guest.PostAsJsonAsync(path+$"/items/{b.Variant}/remove",remove))).Items);Assert.Empty((await Read<CartView>(await guest.PostAsJsonAsync(path+$"/items/{b.Variant}/remove",remove))).Items);
 }
 [Fact]public async Task Concurrent_edits_have_one_winner_and_one_stale_writer()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var client=fixture.Factory.Client(b.Catalog.Token);var cart=await Create(client,b.Catalog.Id);var path=Path(b.Catalog.Id)+$"/{cart.Id}/items";
  var responses=await Task.WhenAll(new[]{2m,3m}.Select(q=>client.PutAsJsonAsync(path,new CartItemRequest(Guid.NewGuid(),b.Variant,q,cart.Version))));Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
 }
 [Fact]public async Task Merge_requires_guest_proof_adds_quantities_once_and_closes_source()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);using var customer=fixture.Factory.Client(b.Catalog.Token,key);
  var source=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,3,true);var target=await Set(customer,b.Catalog.Id,await Create(customer,b.Catalog.Id),b.Variant,2);var request=new MergeCartRequest(Guid.NewGuid(),source.Id,source.Version,target.Version);var path=Path(b.Catalog.Id)+$"/{target.Id}/merge";
  using var attacker=fixture.Factory.Client(b.Catalog.Token,Key());Assert.Equal(HttpStatusCode.NotFound,(await attacker.PostAsJsonAsync(path,request)).StatusCode);
  var merged=await Read<CartView>(await customer.PostAsJsonAsync(path,request));Assert.Equal(5,Assert.Single(merged.Items).Quantity);Assert.Equal(merged.Version,(await Read<CartView>(await customer.PostAsJsonAsync(path,request))).Version);
  Assert.Equal(HttpStatusCode.Conflict,(await guest.GetAsync(Path(b.Catalog.Id,true)+$"/{source.Id}")).StatusCode);
  Assert.Equal(HttpStatusCode.Conflict,(await customer.PostAsJsonAsync(path,request with{OperationId=Guid.NewGuid(),Version=merged.Version})).StatusCode);
 }
 [Fact]public async Task Preview_uses_authoritative_quotes_and_checkout_preparation_checks_live_stock()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var guest=fixture.Factory.Client(capability:Key());var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,2,true);var path=Path(b.Catalog.Id,true)+$"/{cart.Id}";
  var preview=await Read<CartPreview>(await guest.PostAsJsonAsync(path+"/preview",new CartPreviewRequest(Guid.NewGuid())));Assert.True(preview.CanPrepare);Assert.Equal(200,preview.Quote.Calculation.Total);Assert.Equal(10,Assert.Single(preview.Stock).Available);Assert.DoesNotContain("costPrice",JsonSerializer.Serialize(preview),StringComparison.OrdinalIgnoreCase);
  var request=new PrepareCartRequest(Guid.NewGuid(),cart.Version);var prepared=await Read<PreparedCartView>(await guest.PostAsJsonAsync(path+"/prepare",request));Assert.Equal(prepared.Id,(await Read<PreparedCartView>(await guest.PostAsJsonAsync(path+"/prepare",request))).Id);
  cart=await Set(guest,b.Catalog.Id,cart,b.Variant,11,true);Assert.Equal(HttpStatusCode.Conflict,(await guest.PostAsJsonAsync(path+"/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version))).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await guest.PostAsJsonAsync(path+"/prepare",request)).StatusCode);
 }
 [Fact]public async Task Zero_stock_stays_tracked_and_cannot_be_prepared_for_checkout()
 {
  var b=await fixture.Business(0);using var owner=b.Client;using var catalog=b.Catalog.Client;using var guest=fixture.Factory.Client(capability:Key());var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);
  var preview=await Read<CartPreview>(await guest.PostAsJsonAsync(Path(b.Catalog.Id,true)+$"/{cart.Id}/preview",new CartPreviewRequest(Guid.NewGuid())));Assert.False(preview.CanPrepare);Assert.True(Assert.Single(preview.Stock).Tracked);Assert.Equal(0,preview.Stock[0].Available);
 }
 [Fact]public async Task Saved_carts_survive_dependency_outages_but_authoritative_previews_fail_closed()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var guest=fixture.Factory.Client(capability:Key());using var customer=fixture.Factory.Client(b.Catalog.Token);var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var path=Path(b.Catalog.Id,true)+$"/{cart.Id}";
  try{
   fixture.Factory.InventoryOffline=true;Assert.Equal(HttpStatusCode.OK,(await guest.GetAsync(path)).StatusCode);Assert.Equal(HttpStatusCode.ServiceUnavailable,(await guest.PostAsJsonAsync(path+"/preview",new CartPreviewRequest(Guid.NewGuid()))).StatusCode);fixture.Factory.InventoryOffline=false;
   fixture.Factory.PricingOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await guest.PostAsJsonAsync(path+"/preview",new CartPreviewRequest(Guid.NewGuid()))).StatusCode);fixture.Factory.PricingOffline=false;
   fixture.Factory.CatalogOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await guest.PutAsJsonAsync(path+"/items",new CartItemRequest(Guid.NewGuid(),b.Variant,2,cart.Version))).StatusCode);fixture.Factory.CatalogOffline=false;
   fixture.Factory.TenantOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await guest.GetAsync(path)).StatusCode);fixture.Factory.TenantOffline=false;
   fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await customer.PostAsJsonAsync(Path(b.Catalog.Id),new CreateCartRequest(Guid.NewGuid()))).StatusCode);
  }finally{fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=false;fixture.Factory.CatalogOffline=false;fixture.Factory.InventoryOffline=false;fixture.Factory.PricingOffline=false;}
 }
 [Fact]public async Task Internal_preparation_verification_requires_Order_identity_and_matching_guest_proof()
 {
  var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Set(guest,b.Catalog.Id,await Create(guest,b.Catalog.Id,true),b.Variant,1,true);var prepared=await Read<PreparedCartView>(await guest.PostAsJsonAsync(Path(b.Catalog.Id,true)+$"/{cart.Id}/prepare",new PrepareCartRequest(Guid.NewGuid(),cart.Version)));var path=$"/internal/v1/tenants/{b.Catalog.Id}/cart/preparations/{prepared.Id}/verify";
  using var human=fixture.Factory.Client(b.Catalog.Token,key);Assert.Equal(HttpStatusCode.Unauthorized,(await human.PostAsJsonAsync(path,new PreparationProof(null))).StatusCode);
  using var order=fixture.Factory.Client(await fixture.Machine("Order"),key);Assert.Equal(prepared.Id,(await Read<PreparedCartView>(await order.PostAsJsonAsync(path,new PreparationProof(null)))).Id);
  Assert.Equal(HttpStatusCode.NotFound,(await order.PostAsJsonAsync(path,new PreparationProof(Guid.NewGuid()))).StatusCode);order.DefaultRequestHeaders.Remove("X-Cart-Key");Assert.Equal(HttpStatusCode.Unauthorized,(await order.PostAsJsonAsync(path,new PreparationProof(null))).StatusCode);
  fixture.Pricing.Factory.Clock.Advance(TimeSpan.FromMinutes(6));order.DefaultRequestHeaders.Add("X-Cart-Key",key);Assert.Equal(HttpStatusCode.Conflict,(await order.PostAsJsonAsync(path,new PreparationProof(null))).StatusCode);
 }
 [Fact]public async Task Guest_expiry_and_tenant_isolation_are_enforced_without_deleting_history()
 {
  var a=await fixture.Business();var b=await fixture.Business();using var ownerA=a.Client;using var ownerB=b.Client;using var catalogA=a.Catalog.Client;using var catalogB=b.Catalog.Client;var key=Key();using var guest=fixture.Factory.Client(capability:key);var cart=await Create(guest,a.Catalog.Id,true);
  Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync(Path(b.Catalog.Id,true)+$"/{cart.Id}")).StatusCode);using var wrong=fixture.Factory.Client(b.Catalog.Token);Assert.Equal(HttpStatusCode.Forbidden,(await wrong.PostAsJsonAsync(Path(a.Catalog.Id),new CreateCartRequest(Guid.NewGuid()))).StatusCode);
  fixture.Pricing.Factory.Clock.Advance(TimeSpan.FromDays(8));Assert.Equal(HttpStatusCode.Gone,(await guest.GetAsync(Path(a.Catalog.Id,true)+$"/{cart.Id}")).StatusCode);
  using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CartDbContext>();Assert.True(await db.Carts.IgnoreQueryFilters().AnyAsync(c=>c.Id==cart.Id));Assert.Empty(await db.Carts.ToListAsync());
 }
}
