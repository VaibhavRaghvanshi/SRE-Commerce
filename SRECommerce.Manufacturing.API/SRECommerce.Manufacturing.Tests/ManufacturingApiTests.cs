using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Domain;
using SRECommerce.Inventory.Tests;
using SRECommerce.Manufacturing.API;
using SRECommerce.Manufacturing.Application;
using SRECommerce.Manufacturing.Domain;
using SRECommerce.Manufacturing.Infrastructure;
using SRECommerce.Tenant.Application;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace SRECommerce.Manufacturing.Tests;

public sealed class ProductionFactory(InventoryFixture inventory, string secret) : WebApplicationFactory<ManufacturingProgram>
{
    public string DatabaseName { get; } = "SRECommerce_ManufacturingTests_" + Guid.NewGuid().ToString("N");
    public bool LoseReceiptOnce { get; set; } = true;
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:Manufacturing", $"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
        b.UseSetting("Identity:BaseUrl", "https://localhost/");
        b.UseSetting("ServiceAuth:ClientId", ServiceManifest.ClientId("Manufacturing"));
        b.UseSetting("ServiceAuth:ClientSecret", secret);
        b.ConfigureLogging(log => log.SetMinimumLevel(LogLevel.Error));
        b.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ProductionWorker)).ToArray()) services.Remove(d);
            services.AddSingleton<TimeProvider>(inventory.Factory.Clock);
            var tenant = inventory.Catalog.Tenant;
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(() => tenant.Identity.Factory.Server.CreateHandler());
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(() => tenant.Factory.Server.CreateHandler());
            services.AddHttpClient("inventory").ConfigurePrimaryHttpMessageHandler(() => new ReceiptResponseLoss(inventory.Factory.Server.CreateHandler(), this));
        });
    }
    private sealed class ReceiptResponseLoss(HttpMessageHandler inner, ProductionFactory factory) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (factory.LoseReceiptOnce && response.IsSuccessStatusCode && request.RequestUri!.AbsolutePath.EndsWith("/manufacturing-receipts", StringComparison.Ordinal))
            { factory.LoseReceiptOnce = false; response.Dispose(); throw new HttpRequestException("Isolated lost response after stock commit"); }
            return response;
        }
    }
}

public sealed class ManufacturingApiTests
{
    [Fact]
    public async Task ProductionUsesRealInventoryAndReceivesExactlyOnceAfterLostResponse()
    {
        using var inventory = new InventoryFixture();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using (var scope = inventory.Catalog.Tenant.Identity.Factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = new OpenIddictApplicationDescriptor { ClientId = ServiceManifest.ClientId("Manufacturing"), ClientType = ClientTypes.Confidential, ClientSecret = secret };
            descriptor.Permissions.UnionWith([Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials]);
            foreach (var permission in ServiceManifest.Scopes("Manufacturing")) descriptor.Permissions.Add(Permissions.Prefixes.Scope + permission);
            await manager.CreateAsync(descriptor);
        }
        using var factory = new ProductionFactory(inventory, secret);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ManufacturingDbContext>();
            Assert.StartsWith("SRECommerce_ManufacturingTests_", db.Database.GetDbConnection().Database);
            Assert.Equal(factory.DatabaseName, db.Database.GetDbConnection().Database);
            await db.Database.MigrateAsync();
        }
        var business = await inventory.Business();
        using var owner = business.Catalog.Client;
        using var stock = business.Client;
        using var tenantClient = inventory.Catalog.Tenant.Factory.Client(business.Catalog.Token);
        var tenant = business.Catalog.Id;
        var tenantPath = $"/api/v1/tenants/{tenant}";
        var tenantState = await InventoryFixture.Read<TenantView>(await tenantClient.GetAsync(tenantPath));
        await InventoryFixture.Read<TenantView>(await tenantClient.PutAsJsonAsync(tenantPath + "/modules/manufacturing", new ModuleRequest(tenantState.Version, true)));
        var category = await InventoryFixture.Read<CatalogLookup>(await owner.PostAsJsonAsync(tenantPath + "/catalog/categories", new CategoryRequest("Production", "production", null)), HttpStatusCode.Created);
        var draft = new ProductDraft("Finished spice mix", "finished-spice", category.Id, null, ProductKind.Simple, "", "", "", true, false, [], [], [new(null, "FINISHED-1", [])], [], [], []);
        var product = await InventoryFixture.Read<ProductView>(await owner.PostAsJsonAsync(tenantPath + "/catalog/products", draft), HttpStatusCode.Created);
        product = await InventoryFixture.Read<ProductView>(await owner.PostAsJsonAsync(tenantPath + $"/catalog/products/{product.Id}/publish", new SRECommerce.Catalog.Application.VersionRequest(product.Version!)));
        var output = product.Variants[0].Id!.Value;
        var inventoryPath = InventoryFixture.Path(tenant);
        await InventoryFixture.Read<System.Text.Json.JsonElement>(await stock.PostAsJsonAsync(inventoryPath + "/items", new RegisterItemRequest(product.Id, output)));
        await InventoryFixture.Read<BalanceView>(await stock.PostAsJsonAsync(inventoryPath + "/stock-changes",
            new StockChangeRequest(Guid.NewGuid(), business.Warehouse, business.Variant, StockAction.Receipt, 20, "Raw spice received")));
        client.DefaultRequestHeaders.Authorization = new("Bearer", business.Catalog.Token);
        var path = tenantPath + "/manufacturing";
        var bomInput = new BomInput(Guid.NewGuid(), "SPICE", "Spice mix", output, [new(business.Variant, 2, 3)], ["Mix", "Pack"]);
        var bom = await InventoryFixture.Read<BomView>(await client.PostAsJsonAsync(path + "/boms", bomInput));
        Assert.Equal(bom.Id, (await InventoryFixture.Read<BomView>(await client.PostAsJsonAsync(path + "/boms", bomInput))).Id);
        Assert.Equal(2, (await InventoryFixture.Read<BomView>(await client.PostAsJsonAsync(path + "/boms", bomInput with { Id = Guid.NewGuid() }))).Revision);
        var created = await InventoryFixture.Read<ProductionView>(await client.PostAsJsonAsync(path + "/production", new ProductionInput(Guid.NewGuid(), bom.Id, business.Warehouse, 5)));
        Assert.Equal(30, created.StandardMaterialCost);
        var resource = path + "/production/" + created.Id;
        async Task<ProductionView> Read() => await InventoryFixture.Read<ProductionView>(await client.GetAsync(resource));
        async Task Work()
        {
            var worker = new ProductionWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), inventory.Factory.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductionWorker>.Instance);
            await worker.RunOnce(default);
        }
        await InventoryFixture.Read<ProductionView>(await client.PostAsJsonAsync(resource + "/start", new ProductionAction(created.Version, "Start production")), HttpStatusCode.Accepted);
        await Work(); await Work();
        var order = await Read();
        Assert.Equal(ProductionState.Running, order.State); Assert.True(order.InputsConsumed);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(resource + "/cancel", new ProductionAction(order.Version, "Cannot restore processed material"))).StatusCode);
        foreach (var note in new[] { "Mixed ingredients", "Packed batch" })
            order = await InventoryFixture.Read<ProductionView>(await client.PostAsJsonAsync(resource + "/stage", new ProductionAction(order.Version, note)));
        order = await InventoryFixture.Read<ProductionView>(await client.PostAsJsonAsync(resource + "/quality", new QualityInput(order.Version, true, "QC passed")));
        await InventoryFixture.Read<ProductionView>(await client.PostAsJsonAsync(resource + "/finish", new ProductionFinish(order.Version, 4, 1, 2, null, "One unit lost")), HttpStatusCode.Accepted);
        await Work();
        Assert.False(factory.LoseReceiptOnce);
        Assert.Equal(ProductionState.Receiving, (await Read()).State);
        inventory.Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await Work();
        order = await Read(); Assert.Equal(ProductionState.Completed, order.State);
        var balances = await InventoryFixture.Read<BalanceView[]>(await stock.GetAsync(inventoryPath + "/balances"));
        Assert.Equal(4, Assert.Single(balances, x => x.VariantId == output).OnHand);
        Assert.Equal(10, Assert.Single(balances, x => x.VariantId == business.Variant).OnHand);
        Assert.Equal(1, order.WasteQuantity);
    }
}
