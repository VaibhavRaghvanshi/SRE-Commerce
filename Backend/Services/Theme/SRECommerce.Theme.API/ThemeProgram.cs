using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Infrastructure;

namespace SRECommerce.Theme.API;
public partial class ThemeProgram
{
    public static Task Main(string[] args)=>ModuleHost.RunAsync<ThemeDbContext>(args,ServiceManifest.Get("Theme"),typeof(ThemeProgram).Assembly,services=>
    {services.AddScoped<IThemeOperations,ThemeOperations>();services.AddScoped<IModuleInitializer,ThemeInitializer>();});
}
