using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SRECommerce.Api.Auth;
using SRECommerce.Infrastructure.Data;
using Xunit;

namespace SRECommerce.Api.Tests;

public sealed class BaselineTests
{
    private static async Task<JsonElement> Login(HttpClient client, ApiFactory factory, string role = "customer")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{role}@example.test", password = factory.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return body;
    }

    [Fact]
    public async Task Login_preserves_intended_six_hour_lifetime_and_me_subject()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        var auth = await Login(client, factory);
        var expires = auth.GetProperty("expiresAt").GetDateTime().ToUniversalTime();
        Assert.InRange(expires - DateTime.UtcNow, TimeSpan.FromMinutes(359), TimeSpan.FromMinutes(361));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(auth.GetProperty("token").GetString());
        Assert.Equal("3", token.Subject);
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, me.GetProperty("id").GetInt32());
        Assert.Equal("Customer", me.GetProperty("role").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Invalid_password_and_anonymous_user_management_are_denied()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    public async Task Invalid_tokens_cannot_access_me(string scenario)
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        var key = scenario == "wrong-key" ? new string('x', 64) : factory.SigningKey;
        var token = new JwtSecurityToken(
            issuer: scenario == "wrong-issuer" ? "other" : "SRECommerce.Tests",
            audience: scenario == "wrong-audience" ? "other" : "SRECommerce.Tests.Client",
            claims: [new Claim("sub", "1"), new Claim(ClaimTypes.Role, "Admin")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("employee", "GET")]
    [InlineData("employee", "POST")]
    [InlineData("employee", "PUT")]
    [InlineData("employee", "DELETE")]
    [InlineData("customer", "GET")]
    [InlineData("customer", "POST")]
    [InlineData("customer", "PUT")]
    [InlineData("customer", "DELETE")]
    public async Task Non_admin_cannot_read_create_promote_or_delete_accounts(string role, string method)
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory, role);
        var path = method switch { "PUT" => "/api/admin/users/2/role", "DELETE" => "/api/admin/users/3", _ => "/api/admin/users" };
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = method == "PUT" ? JsonContent.Create("Admin") : JsonContent.Create(new { fullName = "Escalation", email = "escalation@example.test", password = factory.Password, role = "Admin" });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Equal("Employee", (await db.Users.FindAsync(2))!.Role);
    }

    [Fact]
    public async Task Admin_can_manage_users_but_cannot_remove_or_demote_last_admin()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory, "admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/admin/users/1/role", "Employee")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/admin/users/1")).StatusCode);
        var create = await client.PostAsJsonAsync("/api/admin/users", new { fullName = "New staff", email = "new@example.test", password = factory.Password, role = "Employee" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/users/{id}/role", "Customer")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task Demoted_admin_is_denied_with_previously_issued_token()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory, "admin");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.FindAsync(1))!.Role = "Employee";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Catalog_home_checkout_history_and_stock_use_server_values()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products?q=Test&page=1&pageSize=12")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/categories/test-category")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/home")).StatusCode);
        await Login(client, factory);
        var response = await client.PostAsJsonAsync("/api/orders", new { customerName = "Buyer", customerEmail = "customer@example.test", phone = "9999999999", shippingAddress = "Test street", items = new[] { new { productId = 1, quantity = 2 } } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(339m, order.GetProperty("total").GetDecimal());
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/orders/mine");
        Assert.Single(mine.EnumerateArray());
        var product = await client.GetFromJsonAsync<JsonElement>("/api/products/test-product");
        Assert.Equal(8, product.GetProperty("stockQty").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/orders", new { customerName = "Buyer", customerEmail = "customer@example.test", phone = "9999999999", shippingAddress = "Test street", items = new[] { new { productId = 1, quantity = 99 } } })).StatusCode);
    }

    [Fact]
    public async Task Health_and_cors_are_explicit_and_no_demo_data_is_added()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        using var bad = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        bad.Headers.Add("Origin", "https://untrusted.example");
        bad.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.False((await client.SendAsync(bad)).Headers.Contains("Access-Control-Allow-Origin"));
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        allowed.Headers.Add("Origin", "http://localhost:4200");
        allowed.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.Equal("http://localhost:4200", (await client.SendAsync(allowed)).Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task Readiness_fails_for_missing_schema_without_seeding()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start(seed: false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void Invalid_lifetime_is_rejected(int minutes)
    {
        var options = new JwtOptions { Issuer = "test", Audience = "test", Key = new string('a', 64), ExpiresMinutes = minutes };
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(options.Validate);
    }

    [Fact]
    public async Task Production_rejects_initialization_and_hides_swagger()
    {
        using var safeFactory = new ApiFactory { EnvironmentName = "Production" };
        using var client = safeFactory.Start();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        using var unsafeFactory = new ApiFactory { EnvironmentName = "Production" };
        unsafeFactory.Overrides["Database:Initialize"] = "true";
        Assert.Throws<InvalidOperationException>(() => unsafeFactory.Start(seed: false));
    }

    [Fact]
    public async Task Admin_summary_executes_on_actual_sqlite_provider()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory, "admin");
        var response = await client.GetAsync("/api/admin/reports/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1255m, report.GetProperty("inventoryValue").GetDecimal());
        Assert.Equal(1, report.GetProperty("totalCustomers").GetInt32());
    }
}
