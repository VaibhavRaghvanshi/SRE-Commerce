using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Application;using SRECommerce.Cart.Infrastructure;
namespace SRECommerce.Cart.API;
public partial class CartProgram
{
 public static Task Main(string[] args)=>ModuleHost.RunAsync<CartDbContext>(args,ServiceManifest.Get("Cart"),typeof(CartProgram).Assembly,services=>
 {
  services.AddSingleton(TimeProvider.System);services.AddScoped<ICartRepository,CartRepository>();services.AddScoped<ICartAccess,CartAccess>();services.AddScoped<ICartService,CartService>();
  foreach(var name in new[]{"Catalog","Inventory","Pricing"})services.AddHttpClient(name.ToLowerInvariant(),(p,c)=>{var config=p.GetRequiredService<IConfiguration>();c.BaseAddress=RemoteAccess.BaseUri(config,name+":BaseUrl",$"http://localhost:{ServiceManifest.Get(name).Port}/",p.GetRequiredService<IHostEnvironment>().IsDevelopment());c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
 });
}
