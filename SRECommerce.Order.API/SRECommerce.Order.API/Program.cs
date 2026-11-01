using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Order.Application;using SRECommerce.Order.Infrastructure;
namespace SRECommerce.Order.API;
public partial class OrderProgram
{
 public static Task Main(string[] args)=>ModuleHost.RunAsync<OrderDbContext>(args,ServiceManifest.Get("Order"),typeof(OrderProgram).Assembly,services=>
 {
  services.AddSingleton(TimeProvider.System);services.AddServiceIdentity();services.AddScoped<IOrderRepository,OrderRepository>();services.AddScoped<IOrderAccess,OrderAccess>();services.AddScoped<IOrderDependencies,OrderDependencies>();services.AddScoped<IOrderSaga,OrderSaga>();services.AddScoped<IOrderWorkflows,OrderWorkflows>();services.AddScoped<IOrderService,OrderService>();services.AddHostedService<OrderRecoveryWorker>();
  foreach(var name in new[]{"Cart","Catalog","Inventory","Pricing","Payment","Shipping","Notification"})services.AddHttpClient(name.ToLowerInvariant(),(p,c)=>{c.BaseAddress=RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(),name+":BaseUrl",$"http://localhost:{ServiceManifest.Get(name).Port}/",p.GetRequiredService<IHostEnvironment>().IsDevelopment());c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
 });
}
