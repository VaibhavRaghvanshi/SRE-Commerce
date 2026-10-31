using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Pricing.Application;
using SRECommerce.Pricing.Domain;
using SRECommerce.Pricing.Infrastructure;
namespace SRECommerce.Pricing.API;
public partial class PricingProgram
{
    public static Task Main(string[] args)=>ModuleHost.RunAsync<PricingDbContext>(args,ServiceManifest.Get("Pricing"),typeof(PricingProgram).Assembly,services=>
    {
        services.AddSingleton(TimeProvider.System);services.AddSingleton<ITaxCalculator,ConfiguredRateTaxCalculator>();services.AddSingleton<PriceEngine>();
        services.AddScoped<IPricingRepository,PricingRepository>();services.AddScoped<IPricingAccess,PricingAccess>();services.AddScoped<IPricingService,PricingService>();
        services.AddHttpClient("catalog",(p,c)=>{var config=p.GetRequiredService<IConfiguration>();c.BaseAddress=RemoteAccess.BaseUri(config,"Catalog:BaseUrl","http://localhost:5085/",p.GetRequiredService<IHostEnvironment>().IsDevelopment());c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
    });
}
