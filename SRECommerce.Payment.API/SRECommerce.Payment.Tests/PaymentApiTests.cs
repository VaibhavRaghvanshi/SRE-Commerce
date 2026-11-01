using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Order.Application;
using SRECommerce.Order.Domain;
using SRECommerce.Payment.Application;
using SRECommerce.Payment.Domain;
using SRECommerce.Payment.Infrastructure;
using SRECommerce.Tenant.Application;
using Xunit;
using static SRECommerce.Payment.Tests.PaymentFixture;

namespace SRECommerce.Payment.Tests;

public sealed class PaymentApiTests(PaymentFixture fixture) : IClassFixture<PaymentFixture>
{
    [Fact]
    public async Task COD_cannot_settle_before_delivery_and_settles_once_after_the_authoritative_callback()
    {
        using var business = await fixture.Business(method: "COD");
        await fixture.Order.Start(business); var checkout = await fixture.Order.Drain(business);
        using var machine = await fixture.Machine(); var path = MachinePath(business.Tenant) + $"/orders/{checkout.Id}";
        var payment = (await machine.GetFromJsonAsync<PaymentView>(path))!; Assert.Equal(PaymentStatus.CashOnDelivery, payment.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await machine.PostAsJsonAsync(path + "/capture", new PaymentVersionRequest(payment.Version))).StatusCode);
        var fulfillment = await Read<FulfillmentView>(await business.Owner.PostAsJsonAsync(SRECommerce.Order.Tests.OrderFixture.Manage(business.Tenant) + $"/orders/{checkout.Id}/fulfillments",
            new FulfillOrderRequest(Guid.NewGuid(), checkout.Order!.Version, business.Warehouse, [new(business.Variant, 2)])));
        await fixture.Order.Work(business.Tenant);
        fulfillment = Assert.Single((await business.Client.GetFromJsonAsync<FulfillmentView[]>(SRECommerce.Order.Tests.OrderFixture.Path(business.Tenant) + $"/{checkout.Id}/fulfillments"))!);
        using var shipping = fixture.Order.Factory.Client(await fixture.Order.Token("Shipping", "internal_order"));
        var callback = $"/internal/v1/tenants/{business.Tenant}/orders/fulfillments/{fulfillment.Id}/status";
        fulfillment = await Read<FulfillmentView>(await shipping.PostAsJsonAsync(callback, new FulfillmentStateRequest(Guid.NewGuid(), fulfillment.Version, FulfillmentStatus.Shipped)));
        await Read<FulfillmentView>(await shipping.PostAsJsonAsync(callback, new FulfillmentStateRequest(Guid.NewGuid(), fulfillment.Version, FulfillmentStatus.Delivered)));
        await fixture.Order.Work(business.Tenant); await fixture.Order.Work(business.Tenant);
        Assert.Equal(PaymentStatus.Captured, (await machine.GetFromJsonAsync<PaymentView>(path))!.Status);
        using var owner = fixture.Factory.Client(business.Pricing.Catalog.Token); var history = await owner.GetFromJsonAsync<JsonElement>(Path(business.Tenant) + $"/orders/{checkout.Id}/history");
        Assert.Equal(2, history.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task Abort_before_payment_initiation_creates_a_fence_against_delayed_start()
    {
        using var business = await fixture.Business(); await fixture.Order.Start(business);
        for (var i = 0; i < 15; i++)
        {
            var checkout = (await business.Client.GetFromJsonAsync<CheckoutView>(SRECommerce.Order.Tests.OrderFixture.Path(business.Tenant) + $"/checkouts/{business.Request.Id}"))!;
            if (checkout.Phase == SagaPhase.StartPayment) break;
            await fixture.Order.Step(business);
        }
        using var machine = await fixture.Machine(); var path = MachinePath(business.Tenant);
        var cancelled = await Read<PaymentView>(await machine.PostAsJsonAsync(path + $"/orders/{business.Request.Id}/abort", new { }));
        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);
        Assert.Equal(cancelled.Version, (await Read<PaymentView>(await machine.PostAsJsonAsync(path, new PaymentStartRequest(business.Request.Id)))).Version);
        var failed = await fixture.Order.Drain(business); Assert.Equal(CheckoutState.Failed, failed.State);
    }

    [Fact]
    public async Task Customer_cannot_manage_or_read_another_customers_payment()
    {
        using var business = await fixture.Business(); await fixture.Order.Start(business); var checkout = await fixture.Order.Drain(business);
        var customer = await fixture.Order.Cart.Pricing.Customer(business.Pricing); using var pricing = customer.Client;
        using var client = fixture.Factory.Client(pricing.DefaultRequestHeaders.Authorization!.Parameter);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path(business.Tenant))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path(business.Tenant) + "/configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Path(business.Tenant) + $"/mine/orders/{checkout.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("Card")]
    [InlineData("UPI")]
    [InlineData("NetBanking")]
    public async Task Real_payment_service_uses_Order_facts_and_captures_once(string method)
    {
        using var business = await fixture.Business(method: method);
        await fixture.Order.Start(business); var checkout = await fixture.Order.Drain(business);
        Assert.Equal(CheckoutState.Accepted, checkout.State);
        using var machine = await fixture.Machine(); var path = MachinePath(business.Tenant);
        var payment = (await machine.GetFromJsonAsync<PaymentView>(path + $"/orders/{checkout.Id}"))!;
        Assert.Equal(PaymentStatus.Captured, payment.Status); Assert.Equal(210, payment.Amount); Assert.Equal(method, payment.Method);
        Assert.True(payment.Sandbox); Assert.Equal("Sandbox", payment.Provider);
        var replay = await Read<PaymentView>(await machine.PostAsJsonAsync(path, new PaymentStartRequest(checkout.Id)));
        Assert.Equal(payment.Version, replay.Version);
        Assert.Equal(payment.Version, (await Read<PaymentView>(await machine.PostAsJsonAsync(path + $"/orders/{checkout.Id}/capture", new PaymentVersionRequest("stale but already captured")))).Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await machine.PostAsJsonAsync(path, new { orderId = checkout.Id, amount = 1, cardNumber = "rejected-field" })).StatusCode);
        using var owner = fixture.Factory.Client(business.Pricing.Catalog.Token);
        var reconciliation = await Read<JsonElement>(await owner.PostAsJsonAsync(Path(business.Tenant) + $"/orders/{checkout.Id}/reconcile", new { }));
        Assert.True(reconciliation.GetProperty("consistent").GetBoolean());
        var history = (await owner.GetFromJsonAsync<JsonElement>(Path(business.Tenant) + $"/orders/{checkout.Id}/history"));
        Assert.Equal(2, history.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task Decline_compensates_the_real_checkout_without_a_capture()
    {
        using var business = await fixture.Business(SandboxOutcome.Declined);
        await fixture.Order.Start(business); var checkout = await fixture.Order.Drain(business);
        Assert.Equal(CheckoutState.Failed, checkout.State);
        using var machine = await fixture.Machine();
        var payment = (await machine.GetFromJsonAsync<PaymentView>(MachinePath(business.Tenant) + $"/orders/{checkout.Id}"))!;
        Assert.Equal(PaymentStatus.Failed, payment.Status); Assert.Equal(0, payment.Refunded);
        using var scope = fixture.Factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Payment");
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>(); Assert.Empty(await db.Attempts.Where(a => a.Action == "captured").ToListAsync());
    }

    [Fact]
    public async Task Refunds_are_partial_bounded_and_idempotent_under_concurrent_requests()
    {
        using var business = await fixture.Business(); await fixture.Order.Start(business); var checkout = await fixture.Order.Drain(business);
        using var machine = await fixture.Machine(); var path = MachinePath(business.Tenant) + $"/orders/{checkout.Id}/refunds";
        var request = new RefundRequest(Guid.NewGuid(), 60, "Approved partial return");
        var first = await Read<PaymentView>(await machine.PostAsJsonAsync(path, request));
        Assert.Equal(PaymentStatus.PartiallyRefunded, first.Status); Assert.Equal(60, first.Refunded);
        Assert.Equal(first.Version, (await Read<PaymentView>(await machine.PostAsJsonAsync(path, request))).Version);
        Assert.Equal(HttpStatusCode.Conflict, (await machine.PostAsJsonAsync(path, request with { Amount = 61 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await machine.PostAsJsonAsync(path, new RefundRequest(Guid.NewGuid(), 151, "Too much"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await machine.PostAsJsonAsync(path, new RefundRequest(Guid.NewGuid(), 0.001m, "Below currency precision"))).StatusCode);
        var responses = await Task.WhenAll(new[] { 100m, 100m }.Select(amount => machine.PostAsJsonAsync(path, new RefundRequest(Guid.NewGuid(), amount, "Concurrent partial refund"))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var final = await Read<PaymentView>(await machine.PostAsJsonAsync(path, new RefundRequest(Guid.NewGuid(), 50, "Final remaining balance")));
        Assert.Equal(PaymentStatus.Refunded, final.Status); Assert.Equal(210, final.Refunded);
    }

    [Fact]
    public async Task Signed_webhooks_verify_body_tenant_amount_timestamp_and_replay_before_checkout_continues()
    {
        using var business = await fixture.Business(SandboxOutcome.Pending, guest: true);
        var pending = await fixture.Pending(business); Assert.Equal(PaymentStatus.Pending, pending.Status);
        var webhook = new SandboxWebhook(Guid.NewGuid(), pending.Id, pending.Currency, pending.Amount, "Authorized");
        using var anonymous = fixture.Factory.Client();
        using (var bad = fixture.Signed(business.Tenant, webhook, bad: true)) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(bad)).StatusCode);
        using (var otherTenant = fixture.Signed(business.Tenant, webhook)) { otherTenant.RequestUri = new Uri($"https://localhost/api/v1/public/tenants/{Guid.NewGuid()}/payments/webhooks/sandbox"); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(otherTenant)).StatusCode); }
        using (var old = fixture.Signed(business.Tenant, webhook, fixture.Order.Cart.Pricing.Factory.Clock.GetUtcNow().AddMinutes(-6).ToUnixTimeSeconds())) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(old)).StatusCode);
        using (var changed = fixture.Signed(business.Tenant, webhook with { Amount = 1 })) Assert.Equal(HttpStatusCode.Conflict, (await anonymous.SendAsync(changed)).StatusCode);
        using (var valid = fixture.Signed(business.Tenant, webhook)) Assert.True((await Read<JsonElement>(await anonymous.SendAsync(valid))).GetProperty("applied").GetBoolean());
        using (var duplicate = fixture.Signed(business.Tenant, webhook)) await Read<JsonElement>(await anonymous.SendAsync(duplicate));
        using (var conflict = fixture.Signed(business.Tenant, webhook with { Outcome = "Declined" })) Assert.Equal(HttpStatusCode.Conflict, (await anonymous.SendAsync(conflict)).StatusCode);
        var checkout = await fixture.Order.Drain(business); Assert.Equal(CheckoutState.Accepted, checkout.State);
    }

    [Fact]
    public async Task Late_webhook_cannot_resurrect_a_cancelled_payment()
    {
        using var business = await fixture.Business(SandboxOutcome.Pending); var pending = await fixture.Pending(business);
        using var machine = await fixture.Machine(); var path = MachinePath(business.Tenant);
        var cancelled = await Read<PaymentView>(await machine.PostAsJsonAsync(path + $"/orders/{pending.OrderId}/abort", new { }));
        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);
        using var anonymous = fixture.Factory.Client(); using var late = fixture.Signed(business.Tenant, new(Guid.NewGuid(), pending.Id, pending.Currency, pending.Amount, "Authorized"));
        Assert.False((await Read<JsonElement>(await anonymous.SendAsync(late))).GetProperty("applied").GetBoolean());
        Assert.Equal(PaymentStatus.Cancelled, (await Read<PaymentView>(await machine.PostAsJsonAsync(path, new PaymentStartRequest(pending.OrderId)))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await machine.PostAsJsonAsync(path + $"/orders/{pending.OrderId}/capture", new PaymentVersionRequest(cancelled.Version))).StatusCode);
        var checkout = await fixture.Order.Drain(business); Assert.Equal(CheckoutState.Failed, checkout.State);
    }

    [Fact]
    public async Task Refund_recovery_remains_available_after_payment_module_is_disabled()
    {
        using var business = await fixture.Business(); await fixture.Order.Start(business); var accepted = await fixture.Order.Drain(business);
        using var tenant = fixture.Order.Cart.Pricing.Catalog.Tenant.Factory.Client(business.Pricing.Catalog.Token);
        var current = (await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{business.Tenant}"))!;
        await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{business.Tenant}/modules/payments", new ModuleRequest(current.Version, false)));
        await Read<OrderView>(await business.Client.PostAsJsonAsync(SRECommerce.Order.Tests.OrderFixture.Path(business.Tenant) + $"/{accepted.Id}/cancel", new CancelOrderRequest(Guid.NewGuid(), accepted.Order!.Version, "Cancel before dispatch")));
        var cancelled = await fixture.Order.Drain(business); Assert.Equal(CheckoutState.Cancelled, cancelled.State);
        Assert.Equal(210, cancelled.Order!.Refunded);
    }

    [Fact]
    public async Task Payment_scope_and_immutable_ledger_reject_human_machine_confusion_and_tampering()
    {
        using var business = await fixture.Business(); await fixture.Order.Start(business); var accepted = await fixture.Order.Drain(business);
        using var owner = fixture.Factory.Client(business.Pricing.Catalog.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync(MachinePath(business.Tenant) + $"/orders/{accepted.Id}")).StatusCode);
        using var wrong = fixture.Factory.Client(await fixture.Order.Token("Shipping", "internal_order"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync(MachinePath(business.Tenant) + $"/orders/{accepted.Id}")).StatusCode);
        using var machine = await fixture.Machine();
        Assert.Equal(HttpStatusCode.NotFound, (await machine.GetAsync(MachinePath(Guid.NewGuid()) + $"/orders/{accepted.Id}")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>(); Assert.Empty(await db.Payments.ToListAsync());
        scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Payment"); var payment = await db.Payments.SingleAsync(); payment.Amount = 1;
        Assert.Equal("PaymentFactsImmutable", (await Assert.ThrowsAsync<BusinessFault>(() => db.SaveChangesAsync())).Code);
        db.ChangeTracker.Clear(); var attempt = await db.Attempts.FirstAsync(); attempt.Action = "rewritten";
        Assert.Equal("PaymentHistoryImmutable", (await Assert.ThrowsAsync<BusinessFault>(() => db.SaveChangesAsync())).Code);
    }
}
