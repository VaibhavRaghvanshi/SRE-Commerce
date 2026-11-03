using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Shipping.Application;using SRECommerce.Shipping.Infrastructure;
namespace SRECommerce.Shipping.API;
public partial class ShippingProgram
{
 public static Task Main(string[] args)=>ModuleHost.RunAsync<ShippingDbContext>(args,ServiceManifest.Get("Shipping"),typeof(ShippingProgram).Assembly,services=>
 {
  services.AddSingleton(TimeProvider.System);services.AddServiceIdentity();services.AddScoped<IShippingRepository,ShippingRepository>();services.AddScoped<IShippingAccess,ShippingAccess>();services.AddScoped<IShippingDependencies,ShippingDependencies>();
  services.AddSingleton<ICourierProvider,SandboxCourierProvider>();services.AddScoped<IShippingService,ShippingService>();services.AddScoped<IShippingCallbacks,ShippingCallbacks>();services.AddHostedService<ShippingCallbackWorker>();
  foreach(var name in new[]{"Order","Cart","Inventory"})services.AddHttpClient(name.ToLowerInvariant(),(p,c)=>{c.BaseAddress=RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(),name+":BaseUrl",$"http://localhost:{ServiceManifest.Get(name).Port}/",p.GetRequiredService<IHostEnvironment>().IsDevelopment());c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
 });
}
