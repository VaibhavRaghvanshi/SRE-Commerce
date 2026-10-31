using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Infrastructure;

namespace SRECommerce.Storefront.API;
public partial class StorefrontProgram
{
    public static Task Main(string[] args)=>ModuleHost.RunAsync<StorefrontDbContext>(args,ServiceManifest.Get("Storefront"),typeof(StorefrontProgram).Assembly,services=>
    {services.AddScoped<IStorefrontOperations,StorefrontOperations>();services.AddScoped<IModuleInitializer,StorefrontInitializer>();});
}
