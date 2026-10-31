using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var connection=Environment.GetEnvironmentVariable("TENANT_MIGRATION_CONNECTION") ?? "Server=localhost,1433;Database=SRECommerce_TenantDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
        var database=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog;
        if(database!="SRECommerce_TenantDB" && !database.StartsWith("SRECommerce_TenantTests_",StringComparison.Ordinal)) throw new InvalidOperationException("Wrong Tenant migration database.");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connection).Options,new DesignCaller(),new TenantDataScope());
    }
}
