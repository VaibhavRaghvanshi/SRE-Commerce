using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Domain;
using SRECommerce.Inventory.Infrastructure;
using SRECommerce.Tenant.Application;
using Xunit;
using static SRECommerce.Inventory.Tests.InventoryFixture;
namespace SRECommerce.Inventory.Tests;
public sealed class InventoryApiTests(InventoryFixture fixture):IClassFixture<InventoryFixture>
{
    private static async Task<BalanceView> Receipt(InventoryBusiness b,decimal quantity,string lot="UNBATCHED",DateTimeOffset? expires=null)=>await Read<BalanceView>(await b.Client.PostAsJsonAsync(Path(b.Catalog.Id)+"/stock-changes",new StockChangeRequest(Guid.NewGuid(),b.Warehouse,b.Variant,StockAction.Receipt,quantity,"Opening stock",Lot:lot,ExpiresAt:expires)));
    private ReserveRequest Hold(InventoryBusiness b,decimal quantity)=>new(Guid.NewGuid(),b.Warehouse,"order-"+Guid.NewGuid().ToString("N"),fixture.Factory.Clock.GetUtcNow().AddMinutes(5),[new(b.Variant,quantity)]);
    [Fact]
    public async Task Concurrent_reservations_cannot_oversell_and_replays_do_not_reserve_twice()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;await Receipt(b,10);var path=Path(b.Catalog.Id);var requests=new[]{Hold(b,7),Hold(b,7)};
        var responses=await Task.WhenAll(requests.Select(r=>client.PostAsJsonAsync(path+"/reservations",r)));Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        var index=Array.FindIndex(responses,r=>r.StatusCode==HttpStatusCode.OK);var first=await Read<ReservationView>(responses[index]);var replay=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",requests[index]));Assert.Equal(first.Id,replay.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/reservations",requests[index] with{Reference="changed"})).StatusCode);
        var balances=(await client.GetFromJsonAsync<BalanceView[]>(path+"/balances"))!;Assert.Equal(7,balances.Sum(s=>s.Reserved));Assert.Equal(3,balances.Sum(s=>s.Available));
    }
    [Fact]
    public async Task Commit_release_and_expiry_have_atomic_idempotent_ledger_transitions()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;await Receipt(b,10);var path=Path(b.Catalog.Id);var hold=Hold(b,4);var reserved=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",hold));
        var committed=await Read<ReservationView>(await client.PostAsJsonAsync(path+$"/reservations/{reserved.Id}/commit",new CloseReservationRequest(reserved.Version)));Assert.Equal(ReservationStatus.Committed,committed.Status);
        await Read<ReservationView>(await client.PostAsJsonAsync(path+$"/reservations/{reserved.Id}/commit",new CloseReservationRequest(reserved.Version)));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+$"/reservations/{reserved.Id}/release",new CloseReservationRequest(committed.Version))).StatusCode);
        var expiring=Hold(b,3);reserved=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",expiring));fixture.Factory.Clock.Advance(TimeSpan.FromMinutes(6));
        var lateReplay=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",expiring));Assert.Equal(ReservationStatus.Expired,lateReplay.Status);
        await Read<JsonElement>(await client.PostAsJsonAsync(path+"/reservations/expire",new{}));var expired=(await client.GetFromJsonAsync<ReservationView>(path+$"/reservations/{reserved.Id}"))!;Assert.Equal(ReservationStatus.Expired,expired.Status);
        var replay=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",expiring));Assert.Equal(ReservationStatus.Expired,replay.Status);
        var balance=Assert.Single((await client.GetFromJsonAsync<BalanceView[]>(path+"/balances"))!);Assert.Equal(6,balance.OnHand);Assert.Equal(0,balance.Reserved);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<InventoryDbContext>();var ledger=await db.Movements.IgnoreQueryFilters().Where(m=>m.TenantId==b.Catalog.Id).ToListAsync();Assert.Equal(balance.OnHand,ledger.Sum(m=>m.OnHandDelta));Assert.Equal(balance.Reserved,ledger.Sum(m=>m.ReservedDelta));
    }
    [Fact]
    public async Task Lots_use_FEFO_and_expired_stock_is_never_available_or_reserved()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;var now=fixture.Factory.Clock.GetUtcNow();var early=await Receipt(b,4,"EARLY",now.AddMinutes(10));await Receipt(b,5,"LATE",now.AddDays(2));var path=Path(b.Catalog.Id);
        var reserved=await Read<ReservationView>(await client.PostAsJsonAsync(path+"/reservations",Hold(b,3)));Assert.Equal(early.Id,Assert.Single(reserved.Items).BalanceId);
        await Read<ReservationView>(await client.PostAsJsonAsync(path+$"/reservations/{reserved.Id}/release",new CloseReservationRequest(reserved.Version)));fixture.Factory.Clock.Advance(TimeSpan.FromMinutes(11));
        using var anonymous=fixture.Factory.Client();var availability=(await anonymous.GetFromJsonAsync<AvailabilityView[]>($"/api/v1/public/tenants/{b.Catalog.Id}/inventory/availability?variants={b.Variant}"))!;Assert.Equal(5,Assert.Single(availability).Available);
        Assert.True(availability[0].Tracked);var unknown=(await anonymous.GetFromJsonAsync<AvailabilityView[]>($"/api/v1/public/tenants/{b.Catalog.Id}/inventory/availability?variants={Guid.NewGuid()}"))!;Assert.False(Assert.Single(unknown).Tracked);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/reservations",Hold(b,6))).StatusCode);
    }
    [Fact]
    public async Task Damage_returns_reconciliation_and_stale_versions_preserve_history()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;var balance=await Receipt(b,10);var path=Path(b.Catalog.Id);var stale=balance.Version;
        var damage=new StockChangeRequest(Guid.NewGuid(),b.Warehouse,b.Variant,StockAction.Damage,3,"Damaged packaging",balance.BatchId,Version:balance.Version);balance=await Read<BalanceView>(await client.PostAsJsonAsync(path+"/stock-changes",damage));Assert.Equal(7,balance.OnHand);Assert.Equal(3,balance.Damaged);
        var repeat=await Read<BalanceView>(await client.PostAsJsonAsync(path+"/stock-changes",damage));Assert.Equal(balance,repeat);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/stock-changes",damage with{Quantity=2})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/stock-changes",new StockChangeRequest(Guid.NewGuid(),b.Warehouse,b.Variant,StockAction.Adjustment,1,"Stale adjustment",balance.BatchId,Version:stale))).StatusCode);
        balance=await Read<BalanceView>(await client.PostAsJsonAsync(path+"/stock-changes",new StockChangeRequest(Guid.NewGuid(),b.Warehouse,b.Variant,StockAction.ReturnToSale,2,"Quality approved",balance.BatchId,Version:balance.Version)));Assert.Equal(9,balance.OnHand);Assert.Equal(1,balance.Damaged);
        balance=await Read<BalanceView>(await client.PostAsJsonAsync(path+"/stock-changes",new StockChangeRequest(Guid.NewGuid(),b.Warehouse,b.Variant,StockAction.Reconciliation,8,"Physical count",balance.BatchId,Version:balance.Version)));Assert.Equal(8,balance.OnHand);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<InventoryDbContext>();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Catalog.Id,"Inventory");var movement=await db.Movements.FirstAsync();movement.Reason="tampered";await Assert.ThrowsAsync<BusinessFault>(()=>db.SaveChangesAsync());
    }
    [Fact]
    public async Task Purchase_partial_receipts_are_idempotent_and_cannot_exceed_ordered_quantity()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);var supplier=await Read<JsonElement>(await client.PostAsJsonAsync(path+"/suppliers",new SupplierRequest("SUP-1","Supplier")),HttpStatusCode.Created);
        var purchase=await Read<PurchaseView>(await client.PostAsJsonAsync(path+"/purchases",new PurchaseRequest(supplier.GetProperty("id").GetGuid(),b.Warehouse,"PO-001","INR",[new(b.Variant,10,12.50m)])),HttpStatusCode.Created);
        purchase=await Read<PurchaseView>(await client.PostAsJsonAsync(path+$"/purchases/{purchase.Id}/submit",new CloseReservationRequest(purchase.Version)));
        var first=new PurchaseReceiveRequest(Guid.NewGuid(),purchase.Version,[new(purchase.Lines[0].Id,4,"PO-LOT",null)]);purchase=await Read<PurchaseView>(await client.PostAsJsonAsync(path+$"/purchases/{purchase.Id}/receive",first));Assert.Equal(PurchaseStatus.PartiallyReceived,purchase.Status);
        var replay=await Read<PurchaseView>(await client.PostAsJsonAsync(path+$"/purchases/{purchase.Id}/receive",first));Assert.Equal(4,replay.Lines[0].Received);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+$"/purchases/{purchase.Id}/receive",new PurchaseReceiveRequest(Guid.NewGuid(),purchase.Version,[new(purchase.Lines[0].Id,7,"PO-LOT",null)]))).StatusCode);
        purchase=await Read<PurchaseView>(await client.PostAsJsonAsync(path+$"/purchases/{purchase.Id}/receive",new PurchaseReceiveRequest(Guid.NewGuid(),purchase.Version,[new(purchase.Lines[0].Id,6,"PO-LOT",null)])));Assert.Equal(PurchaseStatus.Received,purchase.Status);
        Assert.Equal(10,Assert.Single((await client.GetFromJsonAsync<BalanceView[]>(path+"/balances"))!).OnHand);
    }
    [Fact]
    public async Task Tenant_isolation_module_disablement_and_internal_human_separation_are_enforced()
    {
        var a=await fixture.Business();var b=await fixture.Business();using var ac=a.Client;using var bc=b.Client;using var ca=a.Catalog.Client;using var cb=b.Catalog.Client;var stock=await Receipt(a,5);
        ac.DefaultRequestHeaders.Add("X-Tenant-ID",b.Catalog.Id.ToString());Assert.Equal(HttpStatusCode.Forbidden,(await ac.GetAsync(Path(b.Catalog.Id)+"/balances")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await bc.PostAsJsonAsync(Path(b.Catalog.Id)+"/stock-changes",new StockChangeRequest(Guid.NewGuid(),a.Warehouse,b.Variant,StockAction.Receipt,1,"Cross tenant"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await ac.PostAsJsonAsync($"/internal/v1/tenants/{a.Catalog.Id}/inventory/reservations",Hold(a,1))).StatusCode);
        using var tenant=fixture.Catalog.Tenant.Factory.Client(a.Catalog.Token);var view=(await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{a.Catalog.Id}"))!;await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{a.Catalog.Id}/modules/inventory",new ModuleRequest(view.Version,false)));
        Assert.Equal(HttpStatusCode.Forbidden,(await ac.GetAsync(Path(a.Catalog.Id)+"/balances")).StatusCode);
        using var anonymous=fixture.Factory.Client();Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync($"/api/v1/public/tenants/{a.Catalog.Id}/inventory/availability?variants={a.Variant}")).StatusCode);
    }
    [Fact]
    public async Task Dependency_failures_and_unknown_fields_fail_closed()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path+"/branches",new{code="EXTRA",name="Extra",tenantId=Guid.NewGuid()})).StatusCode);
        try{fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(path+"/balances")).StatusCode);fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(path+"/balances")).StatusCode);fixture.Factory.TenantOffline=false;fixture.Factory.CatalogOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync(path+"/items",new RegisterItemRequest(Guid.NewGuid(),Guid.NewGuid()))).StatusCode);}
        finally{fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=false;fixture.Factory.CatalogOffline=false;}
    }
    [Fact]
    public async Task Archived_catalog_products_do_not_expose_stock_through_public_inventory()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;await Receipt(b,5);
        var products=(await catalog.GetFromJsonAsync<SRECommerce.Catalog.Application.CatalogPage<SRECommerce.Catalog.Application.ProductView>>($"/api/v1/tenants/{b.Catalog.Id}/catalog/products"))!;var product=Assert.Single(products.Items);
        Assert.Equal(HttpStatusCode.NoContent,(await catalog.PostAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{product.Id}/archive",new SRECommerce.Catalog.Application.VersionRequest(product.Version!))).StatusCode);
        using var anonymous=fixture.Factory.Client();var result=(await anonymous.GetFromJsonAsync<AvailabilityView[]>($"/api/v1/public/tenants/{b.Catalog.Id}/inventory/availability?variants={b.Variant}"))!;Assert.False(Assert.Single(result).Tracked);Assert.Equal(0,result[0].Available);
        Assert.Equal(5,Assert.Single((await client.GetFromJsonAsync<BalanceView[]>(Path(b.Catalog.Id)+"/balances"))!).OnHand);
    }
    [Fact]
    public async Task Migration_readiness_openapi_and_unbound_query_guards_work()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<InventoryDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Empty(await db.Balances.ToArrayAsync());
        using var client=fixture.Factory.Client();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid())+"/balances")).StatusCode);
    }
}
