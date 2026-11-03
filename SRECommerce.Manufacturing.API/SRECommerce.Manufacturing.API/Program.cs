using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Manufacturing.Application;
using SRECommerce.Manufacturing.Infrastructure;

namespace SRECommerce.Manufacturing.API;

public partial class ManufacturingProgram
{
    public static Task Main(string[] args) => ModuleHost.RunAsync<ManufacturingDbContext>(args, ServiceManifest.Get("Manufacturing"), typeof(ManufacturingProgram).Assembly, services =>
    {
        services.AddServiceIdentity();
        services.AddScoped<IManufacturingAccess, ManufacturingAccess>();
        services.AddScoped<IManufacturingInventory, ManufacturingInventory>();
        services.AddScoped<IManufacturingStore, ManufacturingStore>();
        services.AddScoped<ManufacturingService>();
        services.AddHostedService<ProductionWorker>();
        services.AddHttpClient("inventory", (p, c) =>
        {
            c.BaseAddress = RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(), "Inventory:BaseUrl", "http://localhost:5086/", p.GetRequiredService<IHostEnvironment>().IsDevelopment());
            c.Timeout = TimeSpan.FromSeconds(6);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).WithDependencyResilience();
    });
}
