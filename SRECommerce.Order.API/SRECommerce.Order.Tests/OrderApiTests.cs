using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Order.Application;
using SRECommerce.Order.Domain;
using SRECommerce.Order.Infrastructure;
using Xunit;
using static SRECommerce.Order.Tests.OrderFixture;

namespace SRECommerce.Order.Tests;

public sealed class OrderApiTests(OrderFixture fixture) : IClassFixture<OrderFixture>
{
    [Fact]
    public async Task Recovery_cannot_steal_initial_guest_proof_and_concurrent_lease_claims_serialize()
    {
        using var business=await fixture.Business(guest:true);
        var gate=new InitialProofGate();
        using var gated=fixture.Factory.WithWebHostBuilder(builder=>builder.ConfigureServices(services=>
        {
            services.AddScoped<OrderSaga>();
            services.AddScoped<IOrderSaga>(provider=>new GatedSaga(provider.GetRequiredService<OrderSaga>(),gate));
        }));
        using var client=gated.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        foreach(var header in business.Client.DefaultRequestHeaders)client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key,header.Value);
        var pending=client.PostAsJsonAsync(Path(business.Tenant,true)+"/checkout",business.Request);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            using(var scope=fixture.Factory.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant,"Order");
                var repository=scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var intent=await repository.Intent(business.Request.Id,default);Assert.NotNull(intent);
                Assert.True(intent.NextAttemptAt>=fixture.Cart.Pricing.Factory.Clock.GetUtcNow().AddSeconds(40));
                Assert.DoesNotContain(await repository.Due(fixture.Cart.Pricing.Factory.Clock.GetUtcNow(),default),row=>row.Id==business.Request.Id);
            }
            await fixture.Step(business);
        }
        finally{gate.Release.TrySetResult();}
        var accepted=await Read<CheckoutView>(await pending,HttpStatusCode.Accepted);
        Assert.Equal(CheckoutState.Processing,accepted.State);Assert.Equal(SagaPhase.Validate,accepted.Phase);
        await Task.WhenAll(fixture.Step(business),fixture.Step(business));
        var completed=await fixture.Drain(business);Assert.Equal(CheckoutState.Accepted,completed.State);Assert.NotNull(completed.Order);
        Assert.Equal(business.Request.Id,completed.Order.Id);
    }
    private sealed class InitialProofGate
    {
        public TaskCompletionSource Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class GatedSaga(IOrderSaga inner,InitialProofGate gate):IOrderSaga
    {
        public Task Recover(CancellationToken ct)=>inner.Recover(ct);
        public async Task Run(Guid tenant,Guid id,string? capability,CancellationToken ct,bool interactive=false)
        {if(interactive){gate.Entered.TrySetResult();await gate.Release.Task.WaitAsync(ct);}await inner.Run(tenant,id,capability,ct,interactive);}
    }
    [Fact]
    public async Task Sales_reports_are_manager_only_currency_scoped_and_exclude_pending_orders()
    {
        using var b=await fixture.Business();await fixture.Start(b);var accepted=await fixture.Drain(b);Assert.NotNull(accepted.Order);
        var now=DateOnly.FromDateTime(fixture.Cart.Pricing.Factory.Clock.GetUtcNow().UtcDateTime);
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Order");
            var db=scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Orders.Add(new SalesOrder{TenantId=b.Tenant,Id=Guid.NewGuid(),Number="PENDING-REPORT-TEST",Status=OrderStatus.AwaitingPayment,
                PaymentState=PaymentState.Pending,PaymentMethod="Card",Currency="INR",Subtotal=999,Net=999,ItemsTotal=999,Total=999,
                CreatedAt=fixture.Cart.Pricing.Factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(14)),UpdatedAt=fixture.Cart.Pricing.Factory.Clock.GetUtcNow()});
            await db.SaveChangesAsync();
        }
        var path=Path(b.Tenant)+$"/reports/sales?from={now:yyyy-MM-dd}&through={now:yyyy-MM-dd}&currency=INR";
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Order");
            var direct=await scope.ServiceProvider.GetRequiredService<ISalesReportRepository>().Read(new(now,now),default);
            Assert.Equal(2,direct.Totals.PlacedOrders);
        }
        var report=await Read<SalesReport>(await b.Owner.GetAsync(path));
        Assert.Equal(2,report.Totals.PlacedOrders);Assert.Equal(1,report.Totals.AcceptedOrders);Assert.Equal(accepted.Order.Total,report.Totals.AcceptedOrderTotal);
        Assert.Equal(accepted.Order.Total,report.Totals.CapturedStateBalance);Assert.Single(report.Days);Assert.Single(report.Products);Assert.Equal("UTC",report.TimeZone);
        Assert.Equal(DateTimeKind.Utc,report.Days[0].Date.Kind);Assert.Equal(now,DateOnly.FromDateTime(report.Days[0].Date));
        Assert.Equal(0,(await Read<SalesReport>(await b.Owner.GetAsync(path.Replace("currency=INR","currency=USD")))).Totals.PlacedOrders);
        var ordinary=await fixture.Cart.Pricing.Customer(b.Pricing);using var pricingClient=ordinary.Client;using var customer=fixture.Factory.Client(ordinary.Client.DefaultRequestHeaders.Authorization!.Parameter);
        Assert.Equal(HttpStatusCode.Forbidden,(await customer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await b.Owner.GetAsync(path.Replace("currency=INR","currency=DROP"))).StatusCode);
        var other=await fixture.Business();using(other)Assert.Equal(HttpStatusCode.Forbidden,(await other.Owner.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Guest_can_cancel_a_checkout_before_an_order_exists_with_exact_replay()
    {
        using var business=await fixture.Business(guest:true);
        var created=await fixture.Start(business);Assert.Null(created.Order);
        var path=Path(business.Tenant,true)+$"/checkouts/{business.Request.Id}/cancel";
        var request=new CancelCheckoutRequest("Customer stopped checkout");
        using var stranger=fixture.Factory.Client(orderKey:SRECommerce.Cart.Tests.CartFixture.Key());
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.PostAsJsonAsync(path,request)).StatusCode);
        var queued=await Read<CheckoutView>(await business.Client.PostAsJsonAsync(path,request),HttpStatusCode.Accepted);
        Assert.Equal(CheckoutState.Compensating,queued.State);
        Assert.Equal(queued.State,(await Read<CheckoutView>(await business.Client.PostAsJsonAsync(path,request),HttpStatusCode.Accepted)).State);
        Assert.Equal(HttpStatusCode.Conflict,(await business.Client.PostAsJsonAsync(path,request with{Reason="A different request"})).StatusCode);
        var cancelled=await fixture.Drain(business);
        Assert.Equal(CheckoutState.Cancelled,cancelled.State);Assert.Null(cancelled.Order);
        Assert.Equal(cancelled.State,(await Read<CheckoutView>(await business.Client.PostAsJsonAsync(path,request),HttpStatusCode.Accepted)).State);
    }

    [Fact]
    public async Task Manager_can_resume_reviewed_cancellation_after_a_provider_outage_without_extending_deadline()
    {
        using var business=await fixture.Business();
        await fixture.Start(business);var accepted=await fixture.Drain(business);var order=accepted.Order!;
        var path=Manage(business.Tenant);
        await Read<OrderView>(await business.Owner.PostAsJsonAsync(path+$"/orders/{order.Id}/cancel",new CancelOrderRequest(Guid.NewGuid(),order.Version,"Requested by customer")));
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant,"Order");
            var db=scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var intent=await db.Intents.SingleAsync(x=>x.Id==order.Id);
            intent.Attempts=11;await db.SaveChangesAsync();
        }
        fixture.Providers.PaymentOffline=true;
        try{await fixture.Step(business);}finally{fixture.Providers.PaymentOffline=false;}
        var review=Assert.Single(await Read<CheckoutReview[]>(await business.Owner.GetAsync(path+"/checkout-reviews")));
        Assert.Equal(SagaPhase.CompensatePayment,review.Phase);
        Assert.Equal(12,review.Attempts);
        Assert.Equal(HttpStatusCode.Conflict,(await business.Owner.PostAsJsonAsync(path+$"/checkout-reviews/{order.Id}/recover",new CheckoutRecoveryRequest(Convert.ToBase64String(new byte[8]),false,"Provider back online"))).StatusCode);
        fixture.Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromMinutes(20));
        var resumed=await Read<CheckoutView>(await business.Owner.PostAsJsonAsync(path+$"/checkout-reviews/{order.Id}/recover",new CheckoutRecoveryRequest(review.Version,false,"Provider back online")),HttpStatusCode.Accepted);
        Assert.Equal(OrderStatus.CancellationPending,resumed.Order!.Status);
        Assert.Empty(await Read<CheckoutReview[]>(await business.Owner.GetAsync(path+"/checkout-reviews")));
        var finished=await fixture.Drain(business);
        Assert.Equal(CheckoutState.Cancelled,finished.State);Assert.Equal(order.Total,finished.Order!.Refunded);
        using var finalScope=fixture.Factory.Services.CreateScope();
        finalScope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant,"Order");
        var finalDb=finalScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(review.Deadline,await finalDb.Intents.Where(x=>x.Id==order.Id).Select(x=>x.Deadline).SingleAsync());
        Assert.Equal("Provider back online",await finalDb.Audit.Where(x=>x.Action=="order.checkout.review.reason"&&x.Resource==order.Id.ToString()).Select(x=>x.SupportReason).SingleAsync());
    }

    [Fact]
    public async Task Historical_shipping_and_unknown_payment_address_metadata_are_preserved_without_invented_facts()
    {
        using var business = await fixture.Business();
        var source = new HistoricalSourceInfo(new string('A', 64), "123", "legacy-customer-3", "Pending", "2026-05-31 17:40:50.4673517", "Original unstructured address", false, [new("line-1", "1", "/assets/source.jpg", 0)]);
        var line = new OrderLineView(business.Pricing.Product, business.Variant, "SOURCE-1", "Source item", 1, 0, 0, 0, 0, 100, 100, 0, 100, 0, 100);
        var row = new HistoricalOrderRow("legacy:unknown:1", null, "LEGACY-UNKNOWN-1", OrderStatus.AwaitingPayment, PaymentState.NotRecorded,
            "NotRecorded", "INR", 2, business.Request.Contact with { AddressLine1 = source.ShippingAddress, City = "", CountryCode = "", PostalCode = "" },
            DateTimeOffset.Parse("2026-05-31T17:40:50.4673517Z", System.Globalization.CultureInfo.InvariantCulture), 100, 0, 100, 0, 199, [line], 99, source);
        var path = Manage(business.Tenant) + "/historical-imports";
        var ids = await Read<Guid[]>(await business.Owner.PostAsJsonAsync(path, new HistoricalOrderImport(Guid.NewGuid(), "isolated-legacy", [row])));
        var order = (await business.Owner.GetFromJsonAsync<OrderView>(Manage(business.Tenant) + $"/orders/{Assert.Single(ids)}"))!;
        Assert.Equal(199, order.Total); Assert.Equal(99, order.ShippingTotal); Assert.Equal(100, order.ItemsTotal);
        Assert.Equal(PaymentState.NotRecorded, order.PaymentState); Assert.Equal("NotRecorded", order.PaymentMethod);
        Assert.False(order.TaxDetailsRecorded);
        Assert.Equal("", order.Contact.CountryCode); Assert.Equal("", order.Contact.PostalCode);
        Assert.Equal(source.ShippingAddress, order.Contact.AddressLine1);
        using var scope = fixture.Factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Order");
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>(); var stored = await db.Orders.SingleAsync();
        Assert.Contains("legacy-customer-3", stored.PriceSnapshot); Assert.Null(stored.UserId); Assert.Empty(await db.Outbox.ToListAsync());
        // One malformed row rolls back the entire new batch, including preceding valid rows.
        var invalid = new HistoricalOrderImport(Guid.NewGuid(), "invalid-atomic-batch", [row with { Reference = "new-valid", Number = "NEW-VALID" }, row with { Reference = "new-invalid", Number = "NEW-INVALID", Total = 198 }]);
        Assert.Equal(HttpStatusCode.BadRequest, (await business.Owner.PostAsJsonAsync(path, invalid)).StatusCode);
        Assert.Equal(1, await db.Orders.CountAsync());
    }

    private async Task<FulfillmentView> Deliver(OrderBusiness business, OrderView order, decimal quantity)
    {
        var fulfillment = await Read<FulfillmentView>(await business.Owner.PostAsJsonAsync(
            Manage(business.Tenant) + $"/orders/{order.Id}/fulfillments",
            new FulfillOrderRequest(Guid.NewGuid(), order.Version, business.Warehouse, [new(business.Variant, quantity)])));
        await fixture.Work(business.Tenant);
        fulfillment = Assert.Single((await business.Client.GetFromJsonAsync<FulfillmentView[]>(Path(business.Tenant) + $"/{order.Id}/fulfillments"))!);
        Assert.Equal(FulfillmentStatus.Created, fulfillment.Status);
        var callback = $"/internal/v1/tenants/{business.Tenant}/orders/fulfillments/{fulfillment.Id}/status";
        using var shipping = fixture.Factory.Client(await fixture.Token("Shipping", "internal_order"));
        var shipped = new FulfillmentStateRequest(Guid.NewGuid(), fulfillment.Version, FulfillmentStatus.Shipped, "TEST-DELIVERY");
        Assert.Equal(HttpStatusCode.Unauthorized, (await business.Owner.PostAsJsonAsync(callback, shipped)).StatusCode);
        fulfillment = await Read<FulfillmentView>(await shipping.PostAsJsonAsync(callback, shipped));
        Assert.Equal(fulfillment.Version, (await Read<FulfillmentView>(await shipping.PostAsJsonAsync(callback, shipped))).Version);
        return await Read<FulfillmentView>(await shipping.PostAsJsonAsync(callback,
            new FulfillmentStateRequest(Guid.NewGuid(), fulfillment.Version, FulfillmentStatus.Delivered)));
    }

    [Fact]
    public async Task Partial_delivery_return_is_quantity_bounded_quarantined_and_refunded_at_original_price()
    {
        using var business = await fixture.Business();
        await fixture.Start(business);
        var accepted = await fixture.Drain(business);
        await Deliver(business, accepted.Order!, 1);
        var path = Path(business.Tenant) + $"/{accepted.Id}";
        var order = (await business.Client.GetFromJsonAsync<OrderView>(path))!;
        Assert.Equal(OrderStatus.PartiallyFulfilled, order.Status);
        Assert.Equal(1, Assert.Single(order.Items).Delivered);
        var request = new ReturnOrderRequest(Guid.NewGuid(), order.Version, [new(business.Variant, 1)], "Wrong size");
        var returned = await Read<ReturnView>(await business.Client.PostAsJsonAsync(path + "/returns", request));
        Assert.Equal(100, returned.RefundAmount);
        order = (await business.Client.GetFromJsonAsync<OrderView>(path))!;
        Assert.Equal(HttpStatusCode.Conflict, (await business.Client.PostAsJsonAsync(path + "/returns",
            request with { Id = Guid.NewGuid(), Version = order.Version })).StatusCode);
        var management = Manage(business.Tenant) + $"/returns/{returned.Id}/status";
        returned = await Read<ReturnView>(await business.Owner.PostAsJsonAsync(management,
            new ReturnStateRequest(Guid.NewGuid(), returned.Version, ReturnStatus.Approved)));
        var receive = new ReturnStateRequest(Guid.NewGuid(), returned.Version, ReturnStatus.Received, business.Warehouse);
        returned = await Read<ReturnView>(await business.Owner.PostAsJsonAsync(management, receive));
        Assert.Equal(ReturnStatus.Receiving, returned.Status);
        await fixture.Work(business.Tenant);
        await fixture.Work(business.Tenant);
        await fixture.Work(business.Tenant);
        returned = Assert.Single((await business.Client.GetFromJsonAsync<ReturnView[]>(path + "/returns"))!);
        Assert.Equal(ReturnStatus.Refunded, returned.Status);
        Assert.False(returned.Resalable);
        order = (await business.Client.GetFromJsonAsync<OrderView>(path))!;
        Assert.Equal(100, order.Refunded);
        Assert.Equal(1, Assert.Single(order.Items).Returned);
        Assert.Equal(8, await Available(business));
        using var inventory = fixture.Cart.Inventory.Client(business.Pricing.Catalog.Token);
        var balances = (await inventory.GetFromJsonAsync<SRECommerce.Inventory.Application.BalanceView[]>(
            $"/api/v1/tenants/{business.Tenant}/inventory/balances"))!;
        var quarantine = Assert.Single(balances, b => b.Lot.StartsWith("RETURN-", StringComparison.Ordinal));
        Assert.Equal(1, quarantine.Damaged);
        Assert.Equal(0, quarantine.OnHand);
        lock (fixture.Providers.Gate) Assert.Single(fixture.Providers.Refunds.Values, r => r.Order == order.Id);
    }

    [Fact]
    public async Task COD_is_confirmed_without_capture_and_settled_only_after_delivery_in_the_test_provider()
    {
        using var business = await fixture.Business(method: "COD");
        var captures = fixture.Providers.Captures;
        await fixture.Start(business);
        var accepted = await fixture.Drain(business);
        Assert.Equal(CheckoutState.Accepted, accepted.State);
        Assert.Equal(PaymentState.CashOnDelivery, accepted.Order!.PaymentState);
        Assert.Equal(captures, fixture.Providers.Captures);
        await Deliver(business, accepted.Order, 2);
        await fixture.Work(business.Tenant);
        await fixture.Work(business.Tenant);
        var order = (await business.Client.GetFromJsonAsync<OrderView>(Path(business.Tenant) + $"/{accepted.Id}"))!;
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(PaymentState.Captured, order.PaymentState);
        Assert.Equal(captures + 1, fixture.Providers.Captures);
    }

    [Fact]
    public async Task Historical_import_replays_without_payments_and_rejects_unverified_customer_linking()
    {
        using var business = await fixture.Business();
        var captures = fixture.Providers.Captures;
        var line = new OrderLineView(business.Pricing.Product, business.Variant, "SOURCE-1", "Source item", 1, 1, 1, 0, 0, 100, 100, 0, 100, 0, 100);
        var row = new HistoricalOrderRow("legacy:test:1", null, "LEGACY-TEST-1", OrderStatus.Delivered, PaymentState.Captured,
            "Card", "INR", 2, business.Request.Contact, fixture.Cart.Pricing.Factory.Clock.GetUtcNow().AddDays(-2), 100, 0, 100, 0, 100, [line]);
        var request = new HistoricalOrderImport(Guid.NewGuid(), "isolated-source", [row]);
        var path = Manage(business.Tenant) + "/historical-imports";
        var ids = await Read<Guid[]>(await business.Owner.PostAsJsonAsync(path, request));
        Assert.Equal(ids, await Read<Guid[]>(await business.Owner.PostAsJsonAsync(path, request)));
        Assert.Equal(HttpStatusCode.BadRequest, (await business.Owner.PostAsJsonAsync(path,
            request with { Key = Guid.NewGuid(), Orders = [row with { UserId = Guid.NewGuid() }] })).StatusCode);
        var order = await business.Owner.GetFromJsonAsync<OrderView>(Manage(business.Tenant) + $"/orders/{Assert.Single(ids)}");
        Assert.True(order!.Historical);
        Assert.Equal(captures, fixture.Providers.Captures);
        Assert.Equal(10, await Available(business));
        using var scope = fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Order");
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var source = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Contains("legacy:test:1", source.PriceSnapshot);
        Assert.Null(source.UserId);
        Assert.Empty(await db.Outbox.ToListAsync());
    }

    private async Task<decimal> Available(OrderBusiness business)
    {
        using var inventory = fixture.Cart.Inventory.Client();
        var rows = await inventory.GetFromJsonAsync<SRECommerce.Inventory.Application.AvailabilityView[]>(
            $"/api/v1/public/tenants/{business.Tenant}/inventory/availability?variants={business.Variant}");
        return Assert.Single(rows!).Available;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Checkout_uses_server_snapshots_and_commits_stock_and_payment_once(bool guest)
    {
        using var business = await fixture.Business(guest);
        var captures = fixture.Providers.Captures;
        await fixture.Start(business);
        var completed = await fixture.Drain(business);
        Assert.Equal(CheckoutState.Accepted, completed.State);
        Assert.NotNull(completed.Order);
        Assert.Equal(OrderStatus.Confirmed, completed.Order.Status);
        Assert.Equal(PaymentState.Captured, completed.Order.PaymentState);
        Assert.Equal(200, completed.Order.ItemsTotal);
        Assert.Equal(10, completed.Order.ShippingTotal);
        Assert.Equal(210, completed.Order.Total);
        Assert.Equal(8, await Available(business));
        Assert.Equal(captures + 1, fixture.Providers.Captures);

        var replay = await fixture.Start(business);
        Assert.Equal(completed.Id, replay.Id);
        Assert.Equal(CheckoutState.Accepted, replay.State);
        await fixture.Step(business);
        Assert.Equal(captures + 1, fixture.Providers.Captures);
        Assert.Equal(8, await Available(business));

        using var scope = fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Order");
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.Id == completed.Id));
        Assert.Equal(1, await db.Outbox.CountAsync(o => o.Type == "order.confirmed.v1"));
        var intent = await db.Intents.SingleAsync(i => i.Id == completed.Id);
        if (guest)
        {
            Assert.NotNull(intent.GuestKeyHash);
            Assert.DoesNotContain(business.OrderKey!, intent.RequestDocument);
            Assert.DoesNotContain(business.OrderKey!, intent.CartClaimDocument);
        }
    }

    [Fact]
    public async Task Guest_order_access_requires_its_own_capability_and_rejects_reused_checkout_payloads()
    {
        using var business = await fixture.Business(true);
        await fixture.Start(business);
        await fixture.Drain(business);
        var path = Path(business.Tenant, true);
        using var attacker = fixture.Factory.Client(orderKey: SRECommerce.Cart.Tests.CartFixture.Key());
        using var missing = fixture.Factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync(path + $"/{business.Request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await missing.GetAsync(path + $"/{business.Request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await business.Client.PostAsJsonAsync(path + "/checkout",
            business.Request with { Contact = business.Request.Contact with { City = "Mumbai" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await business.Client.PostAsJsonAsync(path + "/checkout", new
        {
            id = Guid.NewGuid(), business.Request.PreparationId, business.Request.ShippingQuoteId,
            business.Request.PaymentMethod, business.Request.Contact, total = 1m, userId = Guid.NewGuid()
        })).StatusCode);
    }

    [Fact]
    public async Task Declined_payment_releases_held_stock_and_cart_without_capturing()
    {
        using var business = await fixture.Business();
        var captures = fixture.Providers.Captures;
        try
        {
            fixture.Providers.DeclinePayment = true;
            await fixture.Start(business);
            var failed = await fixture.Drain(business);
            Assert.Equal(CheckoutState.Failed, failed.State);
            Assert.Equal("CheckoutFailed", failed.Issue);
            Assert.Equal(OrderStatus.Failed, failed.Order!.Status);
            Assert.Equal(10, await Available(business));
            Assert.Equal(captures, fixture.Providers.Captures);
            var cart = await business.CartClient.GetFromJsonAsync<SRECommerce.Cart.Application.CartView>(
                SRECommerce.Cart.Tests.CartFixture.Path(business.Tenant) + $"/{business.Cart.Id}");
            Assert.Equal(2, Assert.Single(cart!.Items).Quantity);
        }
        finally { fixture.Providers.DeclinePayment = false; }
    }

    [Fact]
    public async Task Cancelled_confirmed_order_refunds_and_restores_original_stock_only_once()
    {
        using var business = await fixture.Business();
        await fixture.Start(business);
        var accepted = await fixture.Drain(business);
        var cancel = new CancelOrderRequest(Guid.NewGuid(), accepted.Order!.Version, "Buyer changed their mind");
        var path = Path(business.Tenant) + $"/{accepted.Id}/cancel";
        var requested = await Read<OrderView>(await business.Client.PostAsJsonAsync(path, cancel));
        Assert.Equal(OrderStatus.CancellationPending, requested.Status);
        var cancelled = await fixture.Drain(business);
        Assert.Equal(CheckoutState.Cancelled, cancelled.State);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Order!.Status);
        Assert.Equal(PaymentState.Refunded, cancelled.Order.PaymentState);
        Assert.Equal(210, cancelled.Order.Refunded);
        Assert.Equal(10, await Available(business));
        Assert.Equal(requested.Version, (await Read<OrderView>(await business.Client.PostAsJsonAsync(path, cancel))).Version);
        await fixture.Step(business);
        Assert.Equal(10, await Available(business));
        lock (fixture.Providers.Gate)
            Assert.Single(fixture.Providers.Refunds.Values, r => r.Order == accepted.Id);
    }

    [Fact]
    public async Task Saved_order_financial_snapshots_and_status_history_are_immutable()
    {
        using var business = await fixture.Business();
        await fixture.Start(business);
        await fixture.Drain(business);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Empty(await db.Orders.ToListAsync());
        scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Order");
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == business.Request.Id);
        order.ContactDocument = "{}";
        Assert.Equal("OrderSnapshotImmutable", (await Assert.ThrowsAsync<BusinessFault>(() => db.SaveChangesAsync())).Code);
        db.ChangeTracker.Clear();
        var item = await db.Items.SingleAsync(i => i.OrderId == business.Request.Id);
        item.UnitPrice = 0;
        Assert.Equal("OrderSnapshotImmutable", (await Assert.ThrowsAsync<BusinessFault>(() => db.SaveChangesAsync())).Code);
        db.ChangeTracker.Clear();
        var history = await db.History.FirstAsync(h => h.OrderId == business.Request.Id);
        history.Reason = "Rewritten source history";
        Assert.Equal("OrderHistoryImmutable", (await Assert.ThrowsAsync<BusinessFault>(() => db.SaveChangesAsync())).Code);
    }
}
