using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SRECommerce.Domain.Common;
using SRECommerce.Domain.Entities;
using SRECommerce.Infrastructure.Data;

namespace SRECommerce.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string TestDirectory { get; } = Path.Combine(Path.GetTempPath(), "SRECommerce.BaselineTests", Guid.NewGuid().ToString("N"));
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string Password { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public string EnvironmentName { get; init; } = "Development";
    public Dictionary<string, string?> Overrides { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.Combine(TestDirectory, "wwwroot"));
        builder.UseEnvironment(EnvironmentName);
        builder.UseWebRoot(Path.Combine(TestDirectory, "wwwroot"));
        // Host configuration is available before Program reads JWT/startup settings.
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(TestDirectory, "test.db")}");
        builder.UseSetting("Jwt:Key", SigningKey);
        builder.UseSetting("Jwt:Issuer", "SRECommerce.Tests");
        builder.UseSetting("Jwt:Audience", "SRECommerce.Tests.Client");
        builder.UseSetting("Jwt:ExpiresMinutes", "360");
        builder.UseSetting("Database:Initialize", "false");
        builder.UseSetting("DemoData:Enabled", "false");
        foreach (var (key, value) in Overrides) builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(TestDirectory, "test.db")}"));
        });
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Error));
    }

    public HttpClient Start(bool seed = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (!seed) return client;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetDbConnection().DataSource != Path.Combine(TestDirectory, "test.db"))
            throw new InvalidOperationException("Refusing to initialize a database outside this test's temporary directory.");
        db.Database.EnsureCreated();
        if (!db.Users.Any())
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
            db.Users.AddRange(
                new User { Id = 1, FullName = "Test Admin", Email = "admin@example.test", Role = Roles.Admin, PasswordHash = hash },
                new User { Id = 2, FullName = "Test Employee", Email = "employee@example.test", Role = Roles.Employee, PasswordHash = hash },
                new User { Id = 3, FullName = "Test Customer", Email = "customer@example.test", Role = Roles.Customer, PasswordHash = hash });
            db.Categories.Add(new Category { Id = 1, Name = "Test Category", Slug = "test-category" });
            db.Products.Add(new Product { Id = 1, Name = "Test Product", Slug = "test-product", Sku = "TEST-1", CategoryId = 1, Price = 125.50m, Discount = 5.50m, StockQty = 10 });
            db.HomeSections.Add(new HomeSection { Type = 3, Title = "Test products", ItemsToShow = 4 });
            db.SaveChanges();
        }
        return client;
    }
}
