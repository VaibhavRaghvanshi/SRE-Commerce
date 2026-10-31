using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Catalog.Infrastructure;
public sealed class CatalogDbContextFactory:IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Catalog"))).Options,new ModuleScope());
}
