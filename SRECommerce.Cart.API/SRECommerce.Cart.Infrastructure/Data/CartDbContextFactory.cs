using Microsoft.EntityFrameworkCore;using Microsoft.EntityFrameworkCore.Design;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Cart.Infrastructure;
public sealed class CartDbContextFactory:IDesignTimeDbContextFactory<CartDbContext>
{public CartDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<CartDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Cart"))).Options,new ModuleScope());}
