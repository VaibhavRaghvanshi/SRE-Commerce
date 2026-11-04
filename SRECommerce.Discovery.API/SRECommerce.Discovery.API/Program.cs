using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Infrastructure;

namespace SRECommerce.Discovery.API;

public partial class DiscoveryProgram
{
    public static Task Main(string[] args) => ModuleHost.RunAsync<DiscoveryDbContext>(args, ServiceManifest.Get("Discovery"), typeof(DiscoveryProgram).Assembly, services =>
    {
        services.AddServiceIdentity();
        services.AddScoped<IDiscoveryAccess, DiscoveryAccess>();
        services.AddScoped<IDiscoverySources, DiscoverySources>();
        services.AddScoped<IDiscoveryStore, DiscoveryStore>();
        services.AddScoped<DiscoveryService>();
        services.AddScoped<IIntegrationHandler, CatalogProjectionHandler>();
        foreach (var name in new[] { "Catalog", "Order" })
            services.AddHttpClient(name.ToLowerInvariant(), (p, c) =>
            {
                c.BaseAddress = RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(), name + ":BaseUrl", $"http://localhost:{ServiceManifest.Get(name).Port}/", p.GetRequiredService<IHostEnvironment>().IsDevelopment());
                c.Timeout = TimeSpan.FromSeconds(6);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).WithDependencyResilience();
    });
}
