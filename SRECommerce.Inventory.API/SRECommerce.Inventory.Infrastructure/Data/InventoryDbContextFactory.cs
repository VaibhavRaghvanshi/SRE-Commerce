using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Inventory.Infrastructure;
public sealed class InventoryDbContextFactory:IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Inventory"))).Options,new ModuleScope());
}
