using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Order.Tests;
using SRECommerce.Payment.API;
using SRECommerce.Payment.Application;
using SRECommerce.Payment.Domain;
using SRECommerce.Payment.Infrastructure;
using Xunit;

namespace SRECommerce.Payment.Tests;

public sealed class PaymentFactory(PaymentFixture fixture) : WebApplicationFactory<PaymentProgram>
{
    public string DatabaseName { get; } = "SRECommerce_PaymentTests_" + Guid.NewGuid().ToString("N");
    public bool OrderOffline { get; set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Payment", $"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
        builder.UseSetting("Identity:BaseUrl", "https://localhost/");
        builder.UseSetting("ServiceAuth:ClientId", ServiceManifest.ClientId("Payment"));
        builder.UseSetting("ServiceAuth:ClientSecret", fixture.Order.ClientSecret("Payment"));
        builder.UseSetting("Sandbox:WebhookSigningKey", fixture.SigningKey);
        builder.UseSetting("RateLimiting:PermitLimit", "1000");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(fixture.Order.Cart.Pricing.Factory.Clock);
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(() => fixture.Order.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(() => fixture.Order.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(() => fixture.Order.Cart.Pricing.Catalog.Tenant.Factory.Server.CreateHandler());
            services.AddHttpClient("order").ConfigurePrimaryHttpMessageHandler(() => new SRECommerce.Catalog.Tests.DependencyTestHandler(fixture.Order.Factory.Server.CreateHandler(), () => OrderOffline));
        });
        builder.ConfigureLogging(o => o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client(string? token = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}

public sealed class PaymentFixture : IDisposable
{
    public OrderFixture Order { get; } = new();
    public PaymentFactory Factory { get; }
    internal string SigningKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public PaymentFixture()
    {
        Factory = new(this); using var client = Factory.Client();
        using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        if (db.Database.GetDbConnection().Database != Factory.DatabaseName || !Factory.DatabaseName.StartsWith("SRECommerce_PaymentTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Wrong Payment test database");
        db.Database.Migrate(); Order.PaymentHandler = () => Factory.Server.CreateHandler();
    }
    public static string Path(Guid tenant) => $"/api/v1/tenants/{tenant}/payments";
    public static string MachinePath(Guid tenant) => $"/internal/v1/tenants/{tenant}/payments";
    public static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    public async Task<OrderBusiness> Business(SandboxOutcome outcome = SandboxOutcome.Authorized, string method = "Card", bool guest = false, bool free = false)
    {
        var business = await Order.Business(guest: guest, method: method, beforePreparation: free ? async pricing =>
        {
            var now = Order.Cart.Pricing.Factory.Clock.GetUtcNow();
            await Read<System.Text.Json.JsonElement>(await pricing.Client.PostAsJsonAsync($"/api/v1/tenants/{pricing.Catalog.Id}/pricing/promotions",
                new SRECommerce.Pricing.Application.PromotionRequest("FREE_TEST", "Explicit development free promotion", SRECommerce.Pricing.Domain.PromotionKind.Percentage, 100, 0, 0, 0, null, [pricing.Variant], null, false, true, 1, now.AddMinutes(-1), now.AddDays(1))), HttpStatusCode.Created);
        } : null);
        if (free) Order.ZeroShippingQuote(business.Request.ShippingQuoteId);
        using var owner = Factory.Client(business.Pricing.Catalog.Token);
        await Read<PaymentConfigView>(await owner.PutAsJsonAsync(Path(business.Tenant) + "/configuration", new PaymentConfigRequest(true, true, true, true, true, outcome)));
        return business;
    }
    public async Task<HttpClient> Machine() => Factory.Client(await Order.Token("Order", "internal_payment"));
    public async Task<PaymentView> Pending(OrderBusiness business)
    {
        await Order.Start(business);
        for (var i = 0; i < 20; i++)
        {
            var checkout = (await business.Client.GetFromJsonAsync<SRECommerce.Order.Application.CheckoutView>(OrderFixture.Path(business.Tenant, business.Guest) + $"/checkouts/{business.Request.Id}"))!;
            if (checkout.Phase == SRECommerce.Order.Domain.SagaPhase.WaitPayment)
            { using var machine = await Machine(); return (await machine.GetFromJsonAsync<PaymentView>(MachinePath(business.Tenant) + $"/orders/{business.Request.Id}"))!; }
            await Order.Step(business);
        }
        throw new InvalidOperationException("Order did not reach payment");
    }
    public HttpRequestMessage Signed(Guid tenant, SandboxWebhook webhook, long? seconds = null, bool bad = false)
    {
        var document = ContractJson.Write(webhook);
        var timestamp = (seconds ?? Order.Cart.Pricing.Factory.Clock.GetUtcNow().ToUnixTimeSeconds()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signer = Factory.Services.GetRequiredService<IPaymentSignatures>();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/public/tenants/{tenant}/payments/webhooks/sandbox") { Content = new StringContent(document, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Sandbox-Timestamp", timestamp); request.Headers.Add("X-Sandbox-Signature", bad ? new string('0', 64) : signer.Sign(tenant, timestamp, document));
        return request;
    }
    public void Dispose() { Factory.Dispose(); Order.Dispose(); }
}
