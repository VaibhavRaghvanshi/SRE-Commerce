using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using SRECommerce.Identity.API;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Infrastructure;
using SRECommerce.BuildingBlocks.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;

if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await client.GetAsync(Environment.GetEnvironmentVariable("IDENTITY_HEALTH_URL") ?? "http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}
var hostArguments = new List<string>();
for (var index=0; index<args.Length; index++)
{
    if (args[index] is "--import-legacy" or "--initialize-service-clients") { index++; continue; }
    if (args[index] is "--initialize" or "--apply-import" or "--link-reviewed-legacy-aliases") continue;
    hostArguments.Add(args[index]);
}
var builder = WebApplication.CreateBuilder(hostArguments.ToArray());
var connection = builder.Configuration.GetConnectionString("Identity") ?? throw new InvalidOperationException("Identity connection is required.");
var sqlOptions = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
var databaseName = sqlOptions.InitialCatalog;
if (!builder.Environment.IsDevelopment() && sqlOptions.TrustServerCertificate)
    throw new InvalidOperationException("Production SQL connections must validate the server certificate.");
if (databaseName != "SRECommerce_IdentityDB" && !databaseName.StartsWith("SRECommerce_IdentityTests_", StringComparison.Ordinal))
    throw new InvalidOperationException("Refusing an Identity connection to a database outside the approved Identity/test namespace.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connection).UseOpenIddict());
var keyPath = builder.Configuration["Identity:DataProtectionPath"];
if (string.IsNullOrWhiteSpace(keyPath))
    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SRECommerce", "Identity", "DataProtection");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("SRECommerce.Identity").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (!builder.Environment.IsDevelopment())
{
    var path = builder.Configuration["Certificates:EncryptionPath"] ?? throw new InvalidOperationException("Production key-ring encryption certificate is required.");
    dataProtection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(path, builder.Configuration["Certificates:EncryptionPassword"]));
}
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Tokens.AuthenticatorIssuer = "SRECommerce";
}).AddEntityFrameworkStores<IdentityDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, LegacyPasswordHasher>();
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.Zero;
    o.OnRefreshingPrincipal = context =>
    {
        // Identity rebuilds the cookie principal after stamp validation. Retain
        // only the authentication-strength claim from the validated old cookie.
        if (context.CurrentPrincipal?.FindFirst("amr") is { } amr
            && context.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identity)
            identity.AddClaim(amr);
        return Task.CompletedTask;
    };
});
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "SRECommerce.Identity.Login";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    o.SlidingExpiration = false;
    o.LoginPath = "/account/login";
});
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
builder.Services.AddScoped<IIdentityAccounts, IdentityAccounts>();
builder.Services.AddHostedService<LocalEmailWorker>();
builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
    .AddServer(o =>
    {
        o.SetIssuer(new Uri(builder.Configuration["Identity:Issuer"]!));
        o.SetAuthorizationEndpointUris("/connect/authorize").SetTokenEndpointUris("/connect/token")
            .SetRevocationEndpointUris("/connect/revoke").SetEndSessionEndpointUris("/connect/logout");
        o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange().AllowRefreshTokenFlow().AllowClientCredentialsFlow();
        o.RegisterScopes(new[]{Scopes.OpenId,Scopes.Profile,Scopes.Email,Scopes.OfflineAccess,"identity_api","tenant_api","commerce_api"}.Concat(ServiceManifest.All.Select(s=>s.InternalScope)).ToArray());
        o.SetAccessTokenLifetime(TimeSpan.FromMinutes(5)).SetRefreshTokenLifetime(TimeSpan.FromDays(7));
        o.SetRefreshTokenReuseLeeway(TimeSpan.Zero);
        o.DisableAccessTokenEncryption();
        if (builder.Environment.IsDevelopment()) o.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
        else
        {
            var signingPath = builder.Configuration["Certificates:SigningPath"] ?? throw new InvalidOperationException("Production signing certificate required.");
            var encryptionPath = builder.Configuration["Certificates:EncryptionPath"] ?? throw new InvalidOperationException("Production encryption certificate required.");
            o.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingPath, builder.Configuration["Certificates:SigningPassword"]));
            o.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionPath, builder.Configuration["Certificates:EncryptionPassword"]));
        }
        var host = o.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough().EnableEndSessionEndpointPassthrough();
        if (builder.Environment.IsDevelopment()) host.DisableTransportSecurityRequirement();
    })
    .AddValidation(o => { o.UseLocalServer(); o.UseAspNetCore(); o.AddAudiences("srecommerce.identity","srecommerce.internal"); o.EnableTokenEntryValidation(); o.EnableAuthorizationEntryValidation(); });
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ActiveSession",p=>p.RequireAuthenticatedUser().AddRequirements(new ActiveSessionRequirement()));
    o.AddPolicy("ServiceAccess",p=>p.RequireAuthenticatedUser().RequireAssertion(context=>
    {
        var name=context.User.GetClaim("service_name");
        return name is not null && ServiceManifest.All.Any(s=>s.Name==name) && context.User.HasClaim("gty","client_credentials")
            && context.User.GetClaim("client_id")==ServiceManifest.ClientId(name) && context.User.GetAudiences().Contains("srecommerce.internal");
    }));
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ActiveSessionHandler>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 40), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<IdentityExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
var app = builder.Build();
var browserFormAction=IdentityBrowserPolicy.FormAction(app.Configuration,app.Environment);
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; form-action "+browserFormAction+"; frame-ancestors 'none'; base-uri 'none'";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseWhen(context=>!context.Request.Path.StartsWithSegments("/health"), branch=>branch.UseHttpsRedirection());
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "Identity" }));
app.MapGet("/health/ready", async (IdentityDbContext db, CancellationToken ct) =>
{
    try { await db.Profiles.OrderBy(x => x.UserId).Select(x => x.UserId).Take(1).ToListAsync(ct); return Results.Ok(new { status = "ready" }); }
    catch (Exception ex) when (ex is not OperationCanceledException) { return Results.StatusCode(503); }
});
if (args.Contains("--initialize") || args.Contains("--import-legacy") || args.Contains("--initialize-service-clients"))
{
    using var scope = app.Services.CreateScope();
    if (args.Contains("--initialize")) await IdentitySetup.InitializeAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    if(args.Contains("--initialize-service-clients"))
    {
        var index=Array.IndexOf(args,"--initialize-service-clients");
        if(index+1>=args.Length)throw new InvalidOperationException("Provide the reviewed private service-client manifest path");
        await ServiceClientProvisioning.RegisterAsync(scope.ServiceProvider,args[index+1]);
    }
    if (args.Contains("--import-legacy"))
    {
        var index = Array.IndexOf(args, "--import-legacy");
        if (index + 1 >= args.Length) throw new InvalidOperationException("Supply a reviewed JSON snapshot path.");
        await IdentitySetup.ImportAsync(scope.ServiceProvider, args[index + 1], args.Contains("--apply-import"), args.Contains("--link-reviewed-legacy-aliases"));
    }
    return;
}
app.Run();
public partial class Program;
