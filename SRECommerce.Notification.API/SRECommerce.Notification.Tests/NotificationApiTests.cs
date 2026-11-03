using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
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
    public TaskCompletionSource<(string? Recipient, string Body)> Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<DeliveryResult> Send(NotificationJob job, CancellationToken ct)
    {
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
        b.ConfigureLogging(log => log.SetMinimumLevel(LogLevel.Error));
        b.ConfigureServices(services =>
        {
            services.AddSingleton<INotificationTransport>(transport);
            var tenant = order.Cart.Pricing.Catalog.Tenant;
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(() => tenant.Factory.Server.CreateHandler());
            services.AddHttpClient("order").ConfigurePrimaryHttpMessageHandler(() => order.Factory.Server.CreateHandler());
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
    }
}
