using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Infrastructure;
namespace SRECommerce.Catalog.API;
public partial class CatalogProgram
{
    public static Task Main(string[] args)=>ModuleHost.RunAsync<CatalogDbContext>(args,ServiceManifest.Get("Catalog"),typeof(CatalogProgram).Assembly,services=>
    {services.AddScoped<ICatalogRepository,CatalogRepository>();services.AddScoped<ICatalogAccess,CatalogAccess>();services.AddScoped<ICatalogService,CatalogService>();});
}
