using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SRECommerce.Identity.API;
using SRECommerce.Identity.Infrastructure;
using Xunit;

namespace SRECommerce.Identity.Tests;

public sealed class IdentityFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = "SRECommerce_IdentityTests_" + Guid.NewGuid().ToString("N");
    public string KeyDirectory { get; } = Path.Combine(Path.GetTempPath(), "SRECommerce.Identity.Tests", Guid.NewGuid().ToString("N"));
    public string AdminEmail { get; } = "admin-" + Guid.NewGuid().ToString("N") + "@example.test";
    public string Password { get; } = "Valid-" + Guid.NewGuid().ToString("N") + "Aa1!";
    public int PermitLimit { get; init; } = 1000;
    public string EnvironmentName { get; init; } = "Development";
    public string Connection => $"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Identity", Connection);
        builder.UseSetting("Identity:Issuer", "https://localhost/");
        builder.UseSetting("Identity:DataProtectionPath", KeyDirectory);
        builder.UseSetting("Bootstrap:Email", AdminEmail);
        builder.UseSetting("Bootstrap:Password", Password);
        builder.UseSetting("RateLimiting:PermitLimit", PermitLimit.ToString());
        builder.UseSetting("Mailpit:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();
            services.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(Connection).UseOpenIddict());
        });
        builder.ConfigureLogging(o => o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
}

public sealed class IdentityFixture : IDisposable
{
    public IdentityFactory Factory { get; } = new();
    public IdentityFixture()
    {
        using var client = Factory.Client();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var actual = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
        if (actual != Factory.DatabaseName || !actual.StartsWith("SRECommerce_IdentityTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing migration outside the isolated test database.");
        db.Database.Migrate();
        IdentitySetup.InitializeAsync(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IConfiguration>(), scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>()).GetAwaiter().GetResult();
        Console.WriteLine("Isolated SQL Server integration database: " + Factory.DatabaseName);
    }
    public void Dispose() => Factory.Dispose(); // Keep SQL database for inspection; never drop source/test data implicitly.
}

public record TokenSet(HttpClient Client, string Access, string Refresh, string IdToken);
public static class Protocol
{
    public static string Password() => "Valid-" + Guid.NewGuid().ToString("N") + "Aa1!";
    public static async Task<TokenSet> SignIn(IdentityFactory factory, string email, string password, Guid? tenant = null, string? mfaCode = null, bool recovery = false)
    {
        var client = factory.Client();
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var authorize = "/connect/authorize?" + new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "srecommerce-local", ["redirect_uri"] = "http://127.0.0.1:5099/callback", ["response_type"] = "code",
            ["scope"] = "openid profile email offline_access identity_api", ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["state"] = state, ["nonce"] = Guid.NewGuid().ToString("N"), ["tenant_id"] = tenant?.ToString() ?? ""
        }).ReadAsStringAsync().Result;
        var initial = await client.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, initial.StatusCode);
        var login = await client.GetStringAsync("/account/login?returnUrl=" + Uri.EscapeDataString(authorize));
        var csrf = Csrf(login);
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email, ["password"] = password, ["returnUrl"] = authorize, ["__RequestVerificationToken"] = csrf
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        if (mfaCode is not null)
        {
            var mfaPage = await client.GetStringAsync(response.Headers.Location!);
            response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["mfa"] = "true", ["recovery"] = recovery.ToString().ToLowerInvariant(), ["code"] = mfaCode, ["returnUrl"] = authorize, ["__RequestVerificationToken"] = Csrf(mfaPage)
            }));
        }
        Assert.Equal(authorize, response.Headers.Location?.OriginalString);
        var authorization = await client.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, authorization.StatusCode);
        var location = authorization.Headers.Location!;
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(state, query["state"].ToString());
        Assert.True(query.ContainsKey("code"), "Authorization response did not contain a code: " + location.GetLeftPart(UriPartial.Path));
        var exchange = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = "srecommerce-local", ["redirect_uri"] = "http://127.0.0.1:5099/callback", ["code"] = query["code"].ToString(), ["code_verifier"] = verifier
        }));
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var tokens = await exchange.Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return new(client, access, tokens.GetProperty("refresh_token").GetString()!, tokens.GetProperty("id_token").GetString()!);
    }
    public static string Csrf(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
    public static string Totp(string key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(key.ToUpperInvariant().TrimEnd('=').Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        var secret = Enumerable.Range(0, bits.Length/8).Select(i => Convert.ToByte(bits.Substring(i*8, 8), 2)).ToArray();
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(secret, counter);
        var index = hash[^1] & 15;
        var number = ((hash[index]&127)<<24)|(hash[index+1]<<16)|(hash[index+2]<<8)|hash[index+3];
        return (number%1000000).ToString("D6");
    }
}
