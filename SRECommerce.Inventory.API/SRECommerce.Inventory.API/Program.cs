using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Infrastructure;
namespace SRECommerce.Inventory.API;
public partial class InventoryProgram
{
    public static Task Main(string[] args)=>ModuleHost.RunAsync<InventoryDbContext>(args,ServiceManifest.Get("Inventory"),typeof(InventoryProgram).Assembly,services=>
    {
        services.AddSingleton(TimeProvider.System);services.AddScoped<IInventoryRepository,InventoryRepository>();services.AddScoped<IInventoryAccess,InventoryAccess>();
        services.AddScoped<InventoryService>();services.AddScoped<IInventoryService>(p=>p.GetRequiredService<InventoryService>());services.AddScoped<IInventoryMaintenance>(p=>p.GetRequiredService<InventoryService>());
        services.AddHttpClient("catalog",(p,c)=>{var config=p.GetRequiredService<IConfiguration>();c.BaseAddress=RemoteAccess.BaseUri(config,"Catalog:BaseUrl","http://localhost:5085/",p.GetRequiredService<IHostEnvironment>().IsDevelopment());c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
        services.AddHostedService<ReservationExpiryWorker>();
    });
}
