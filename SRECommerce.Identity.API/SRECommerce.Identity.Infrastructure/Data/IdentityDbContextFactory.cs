using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_MIGRATION_CONNECTION")
            ?? "Server=localhost,1433;Database=SRECommerce_IdentityDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).UseOpenIddict().Options, new NoTenantContext());
    }
}
