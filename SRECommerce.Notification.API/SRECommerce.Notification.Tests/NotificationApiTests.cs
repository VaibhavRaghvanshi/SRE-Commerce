using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Cart.Application;
using SRECommerce.Cart.Infrastructure;
using SRECommerce.Notification.API;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;
using SRECommerce.Notification.Infrastructure;
using SRECommerce.Order.Infrastructure;
using SRECommerce.Order.Tests;
using SRECommerce.Tenant.Application;
using Xunit;

namespace SRECommerce.Notification.Tests;

public sealed class NoticeTestTransport : INotificationTransport
{
    public bool Available => true;
    public System.Collections.Concurrent.ConcurrentQueue<string?> Recipients { get; } = new();
    public TaskCompletionSource<(string? Recipient, string Body)> Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<DeliveryResult> Send(NotificationJob job, CancellationToken ct)
    {
        Recipients.Enqueue(job.Recipient);
        Delivered.TrySetResult((job.Recipient, job.Body));
        return Task.FromResult(new DeliveryResult(DeliveryState.Sent, "IsolatedTransport"));
    }
}

public sealed class NoticeFactory(OrderFixture order, NoticeTestTransport transport) : WebApplicationFactory<NotificationProgram>
{
    public string DatabaseName { get; } = "SRECommerce_NotificationTests_" + Guid.NewGuid().ToString("N");
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:Notification", $"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
        b.UseSetting("Identity:BaseUrl", "https://localhost/");
        b.UseSetting("ServiceAuth:ClientId", ServiceManifest.ClientId("Notification"));
        b.UseSetting("ServiceAuth:ClientSecret", order.ClientSecret("Notification"));
        b.UseSetting("CartReminders:StorefrontOrigin", "http://localhost:4000/");
        b.ConfigureLogging(log => log.SetMinimumLevel(LogLevel.Error));
        b.ConfigureServices(services =>
        {
            services.AddSingleton<INotificationTransport>(transport);
            var tenant = order.Cart.Pricing.Catalog.Tenant;
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(() => tenant.Factory.Server.CreateHandler());
            services.AddHttpClient("order").ConfigurePrimaryHttpMessageHandler(() => order.Factory.Server.CreateHandler());
            services.AddHttpClient("cart").ConfigurePrimaryHttpMessageHandler(() => order.Cart.Factory.Server.CreateHandler());
        });
    }
    public HttpClient Client(string token)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}

public sealed class NotificationApiTests
{
    [Fact]
    public async Task SavedCartReminderWaitsForInactivityAndRechecksOptOutAndCheckout()
    {
        using var order = new OrderFixture();
        var notificationToken = await order.Token("Notification", "internal_cart");
        var transport = new NoticeTestTransport();
        using var factory = new NoticeFactory(order, transport);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            Assert.Equal(factory.DatabaseName, db.Database.GetDbConnection().Database);
            await db.Database.MigrateAsync();
        }
        var business = await order.Cart.Business();
        using var pricing = business.Client;
        using var catalog = business.Catalog.Client;
        var tenantId = business.Catalog.Id;
        using var tenant = order.Cart.Pricing.Catalog.Tenant.Factory.Client(business.Catalog.Token);
        var tenantPath = $"/api/v1/tenants/{tenantId}";
        var tenantView = await OrderFixture.Read<TenantView>(await tenant.GetAsync(tenantPath));
        await OrderFixture.Read<TenantView>(await tenant.PutAsJsonAsync(tenantPath + "/modules/notifications", new ModuleRequest(tenantView.Version, true)));

        var carts = new List<CartView>();
        var users = new List<HttpClient>();
        var cartPath = SRECommerce.Cart.Tests.CartFixture.Path(tenantId);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var customer = await order.Cart.Pricing.Customer(business);
                using var pricingClient = customer.Client;
                var client = order.Cart.Factory.Client(pricingClient.DefaultRequestHeaders.Authorization!.Parameter);
                users.Add(client);
                var cart = await OrderFixture.Read<CartView>(await client.PostAsJsonAsync(cartPath, new CreateCartRequest(Guid.NewGuid())));
                var path = $"{cartPath}/{cart.Id}/reminder";
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new CartReminderRequest(true))).StatusCode);
                cart = await OrderFixture.Read<CartView>(await client.PutAsJsonAsync($"{cartPath}/{cart.Id}/items",
                    new CartItemRequest(Guid.NewGuid(), business.Variant, 1, cart.Version)));
                carts.Add(cart);
                Assert.True((await OrderFixture.Read<CartReminderPreference>(await client.PutAsJsonAsync(path, new CartReminderRequest(true)))).Enabled);
                if (i > 0) Assert.Equal(HttpStatusCode.NotFound, (await users[0].GetAsync(path)).StatusCode);
            }
            var scheduler = order.Cart.Factory.Services.GetRequiredService<CartReminderQueue>();
            using var cartScope = order.Cart.Factory.Services.CreateScope();
            cartScope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenantId, "Cart");
            var cartDb = cartScope.ServiceProvider.GetRequiredService<CartDbContext>();
            await scheduler.QueueDue(default);
            Assert.False(await cartDb.Outbox.AnyAsync(row => row.Type == MessagingTopology.CartReminderDue));
            order.Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromMinutes(1)));
            await scheduler.QueueDue(default);
            await scheduler.QueueDue(default);
            var envelopes = (await cartDb.Outbox.Where(row => row.Type == MessagingTopology.CartReminderDue)
                .Select(row => row.Envelope).ToArrayAsync()).Select(ContractJson.Read<EventEnvelope>).ToArray();
            Assert.Equal(3, envelopes.Length);

            var cancelled = $"{cartPath}/{carts[1].Id}/reminder";
            Assert.False((await OrderFixture.Read<CartReminderPreference>(await users[1].PutAsJsonAsync(cancelled, new CartReminderRequest(false)))).Enabled);
            Assert.Equal(HttpStatusCode.Conflict, (await users[1].PutAsJsonAsync(cancelled, new CartReminderRequest(true))).StatusCode);
            var checkedOut = await cartDb.Carts.SingleAsync(row => row.Id == carts[2].Id);
            checkedOut.Status = SRECommerce.Cart.Domain.CartStatus.CheckedOut;
            await cartDb.SaveChangesAsync();

            var first = envelopes.Single(row => row.Data.GetProperty("cartId").GetGuid() == carts[0].Id);
            var firstUser = first.Data.GetProperty("userId").GetGuid();
            using var machine = order.Cart.Factory.Client(notificationToken);
            var proof = $"/internal/v1/tenants/{tenantId}/cart/reminders/{first.Id}/eligibility?cartId={carts[0].Id}&userId={firstUser}";
            Assert.Equal(HttpStatusCode.NoContent, (await machine.GetAsync(proof)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await order.Cart.Factory.Client(await order.Token("Order", "internal_cart")).GetAsync(proof)).StatusCode);

            using var work = factory.Services.CreateScope();
            work.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenantId, "Cart");
            var handler = work.ServiceProvider.GetServices<IIntegrationHandler>().OfType<CartReminderHandler>().Single();
            foreach (var envelope in envelopes)
            {
                await handler.HandleAsync(envelope, default);
                await handler.HandleAsync(envelope, default);
            }
            var delivered = await transport.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(45));
            Assert.Contains("http://localhost:4000/stores/", delivered.Body);
            Assert.EndsWith("/cart", delivered.Body);
            var notices = work.ServiceProvider.GetRequiredService<NotificationDbContext>();
            NotificationJob[] jobs = [];
            for (var attempt = 0; attempt < 30; attempt++)
            {
                jobs = await notices.Jobs.AsNoTracking().ToArrayAsync();
                if (jobs.Length == 3 && jobs.All(row => row.State is DeliveryState.Sent or DeliveryState.Suppressed)) break;
                await Task.Delay(500);
            }
            Assert.Equal(3, jobs.Length);
            Assert.Equal(DeliveryState.Sent, jobs.Single(row => row.ResourceId == carts[0].Id).State);
            Assert.All(jobs.Where(row => row.ResourceId != carts[0].Id), row => Assert.Equal(DeliveryState.Suppressed, row.State));
            Assert.All(jobs, row => Assert.Null(row.Recipient));
            Assert.Single(transport.Recipients);
        }
        finally { foreach (var user in users) user.Dispose(); }
    }

    [Fact]
    public async Task OrderProofTenantScopeAndInboxReplayProtectDeliveryAndRecipient()
    {
        using var order = new OrderFixture();
        _ = await order.Token("Notification", "internal_order");
        var transport = new NoticeTestTransport();
        using var factory = new NoticeFactory(order, transport);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            Assert.StartsWith("SRECommerce_NotificationTests_", db.Database.GetDbConnection().Database);
            Assert.Equal(factory.DatabaseName, db.Database.GetDbConnection().Database);
            await db.Database.MigrateAsync();
        }
        using var business = await order.Business();
        using var tenant = order.Cart.Pricing.Catalog.Tenant.Factory.Client(business.Pricing.Catalog.Token);
        var tenantPath = $"/api/v1/tenants/{business.Tenant}";
        var tenantView = await OrderFixture.Read<TenantView>(await tenant.GetAsync(tenantPath));
        await OrderFixture.Read<TenantView>(await tenant.PutAsJsonAsync(tenantPath + "/modules/notifications", new ModuleRequest(tenantView.Version, true)));
        using var owner = factory.Client(business.Pricing.Catalog.Token);
        using var machine = factory.Client(await order.Token("Order", "internal_notification"));
        var management = tenantPath + "/notifications";
        var queue = $"/internal/v1/tenants/{business.Tenant}/notifications/order";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(management + "/templates/0/0", new TemplateInput("Order", "{{contactEmail}}", true, null))).StatusCode);
        var template = await OrderFixture.Read<TemplateView>(await owner.PutAsJsonAsync(management + "/templates/0/0", new TemplateInput("Order confirmed", "Order {{orderNumber}} is confirmed.", true, null)));
        Assert.NotEmpty(template.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(management + "/templates/0/0", new TemplateInput("Order confirmed", "Valid body", true, Convert.ToBase64String(new byte[8])))).StatusCode);
        await order.Start(business);
        var checkout = await order.Drain(business);
        Assert.Equal(SRECommerce.Order.Domain.CheckoutState.Accepted, checkout.State);
        Guid eventId;
        using (var scope = order.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Order");
            eventId = await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Outbox
                .Where(x => x.Type == OrderNoticesContract.Types[0]).Select(x => x.Id).SingleAsync();
        }
        var notice = new OrderNoticeRequest(eventId, business.Request.Id, NotificationKind.OrderConfirmed);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync(queue, notice)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await machine.PostAsJsonAsync(queue, notice with { EventId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await machine.PostAsJsonAsync(queue.Replace(business.Tenant.ToString(), Guid.NewGuid().ToString()), notice)).StatusCode);
        var queued = Assert.Single(await OrderFixture.Read<JobView[]>(await machine.PostAsJsonAsync(queue, notice)));
        Assert.Equal(eventId, queued.EventId);
        Assert.Equal(HttpStatusCode.Conflict, (await machine.PostAsJsonAsync(queue, notice with { Kind = NotificationKind.OrderCancelled })).StatusCode);
        var delivered = await transport.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(business.Request.Contact.Email, delivered.Recipient);
        Assert.Contains(checkout.Order!.Number, delivered.Body);
        AttemptView[] attempts = [];
        for (var i = 0; i < 20 && attempts.Length == 0; i++)
        {
            attempts = await OrderFixture.Read<AttemptView[]>(await owner.GetAsync(management + $"/jobs/{eventId}/0/attempts"));
            if (attempts.Length == 0) await Task.Delay(100);
        }
        Assert.Equal(DeliveryState.Sent, Assert.Single(attempts).State);
        Assert.Single(await OrderFixture.Read<JobView[]>(await machine.PostAsJsonAsync(queue, notice)));
        Assert.Single(await OrderFixture.Read<JobView[]>(await owner.GetAsync(management + "/jobs")));
        using var finalScope = factory.Services.CreateScope();
        finalScope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant, "Notification");
        Assert.Null(await finalScope.ServiceProvider.GetRequiredService<NotificationDbContext>().Jobs.Select(x => x.Recipient).SingleAsync());

        await OrderFixture.Read<TemplateView>(await owner.PutAsJsonAsync(management+"/templates/5/0",new TemplateInput("Low stock: {{sku}}","{{available}} available; reorder at {{reorderLevel}}.",true,null)));
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PutAsJsonAsync(management+"/templates/5/1",new TemplateInput("Low stock","Stock notice",true,null))).StatusCode);
        var subscription=await OrderFixture.Read<StockSubscriptionView>(await owner.PutAsJsonAsync(management+"/stock-alerts",new StockSubscriptionInput(true,1440,null)));
        Assert.True(subscription.Enabled);
        var inventoryEvent=new EventEnvelope(Guid.NewGuid(),1,"Inventory","inventory.low-stock.v1",business.Tenant,"stock-test",DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new StockAlertEvent(business.Variant,"LOW-STOCK-TEST",1,2,1,DateTimeOffset.UtcNow),ContractJson.Strict));
        async Task Receive(EventEnvelope message)
        {
            using var delivery=factory.Services.CreateScope();delivery.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(message.TenantId,"Inventory");
            await delivery.ServiceProvider.GetRequiredService<LowStockNotificationHandler>().HandleAsync(message,default);
        }
        await Receive(inventoryEvent);await Receive(inventoryEvent);await Receive(inventoryEvent with{Id=Guid.NewGuid()});
        using var stockScope=factory.Services.CreateScope();stockScope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(business.Tenant,"Notification");
        var stockDb=stockScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var stockJob=await stockDb.Jobs.SingleAsync(row=>row.Kind==NotificationKind.LowStock);
        Assert.Equal(business.Variant,stockJob.ResourceId);Assert.Contains("LOW-STOCK-TEST",stockJob.Subject);Assert.Contains("1 available",stockJob.Body);
        Assert.Single(await stockDb.StockReceipts.ToArrayAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PutAsJsonAsync(management+"/stock-alerts",new StockSubscriptionInput(false,1440,Convert.ToBase64String(new byte[8])))).StatusCode);
        await OrderFixture.Read<StockSubscriptionView>(await owner.PutAsJsonAsync(management+"/stock-alerts",new StockSubscriptionInput(false,1440,subscription.Version)));
    }
}
