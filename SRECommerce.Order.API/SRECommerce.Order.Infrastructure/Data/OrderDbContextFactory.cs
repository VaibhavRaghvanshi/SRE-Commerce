using Microsoft.EntityFrameworkCore;using Microsoft.EntityFrameworkCore.Design;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Order.Infrastructure;
public sealed class OrderDbContextFactory:IDesignTimeDbContextFactory<OrderDbContext>
{public OrderDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<OrderDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Order"))).Options,new ModuleScope());}
