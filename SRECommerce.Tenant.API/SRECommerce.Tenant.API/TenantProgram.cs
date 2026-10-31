using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;
using SRECommerce.Tenant.Infrastructure;

namespace SRECommerce.Tenant.API;

public partial class TenantProgram
{
    public static async Task Main(string[] args)
    {
        if(args.Contains("--healthcheck"))
        {
            try {using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Environment.ExitCode=(await http.GetAsync(Environment.GetEnvironmentVariable("TENANT_HEALTH_URL")??"http://127.0.0.1:8080/health/ready")).IsSuccessStatusCode?0:1;}
            catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException){Environment.ExitCode=1;}return;
        }
        var builder=WebApplication.CreateBuilder(args.Where(a=>a!="--initialize").ToArray());
        var connection=builder.Configuration.GetConnectionString("Tenant")??throw new InvalidOperationException("Tenant connection required.");
        var sql=new SqlConnectionStringBuilder(connection);
        if(sql.InitialCatalog!="SRECommerce_TenantDB" && !sql.InitialCatalog.StartsWith("SRECommerce_TenantTests_",StringComparison.Ordinal)) throw new InvalidOperationException("Wrong Tenant database.");
        if(!builder.Environment.IsDevelopment() && sql.TrustServerCertificate) throw new InvalidOperationException("Production SQL must validate its certificate.");
        var identityUri=new Uri(builder.Configuration["Identity:BaseUrl"]??throw new InvalidOperationException("Trusted Identity base URI required."));
        if(identityUri.Scheme!="https" && (!builder.Environment.IsDevelopment() || !new[]{"localhost","127.0.0.1","identity","host.docker.internal"}.Contains(identityUri.Host))) throw new InvalidOperationException("Identity requires trusted HTTPS outside local development.");
        if(!string.IsNullOrEmpty(identityUri.UserInfo) || !string.IsNullOrEmpty(identityUri.Query) || !identityUri.AbsoluteUri.EndsWith('/')) throw new InvalidOperationException("Invalid Identity base URI.");
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ITenantCaller,CallerContext>();builder.Services.AddScoped<TenantDataScope>();
        // Tenant has no cookies or protected persistent payloads; Identity owns
        // session cryptography. Avoid unused keys in the container filesystem.
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddDbContext<TenantDbContext>(o=>o.UseSqlServer(connection));
        builder.Services.AddHttpClient<IdentityConnection>(http=>{http.BaseAddress=identityUri;http.Timeout=TimeSpan.FromSeconds(5);})
            .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
        builder.Services.AddScoped<IIdentityOwnerProvisioner>(p=>p.GetRequiredService<IdentityConnection>());
        builder.Services.AddSingleton<IDomainOwnershipVerifier,DnsOwnershipVerifier>();builder.Services.AddScoped<ITenantOperations,TenantOperations>();
        builder.Services.AddScoped<ITenantRegistry,TenantRegistry>();
        builder.Services.AddAuthentication("IdentityContext").AddScheme<AuthenticationSchemeOptions,IdentityContextHandler>("IdentityContext",_=>{});
        builder.Services.AddAuthorization();builder.Services.AddControllers();builder.Services.AddOpenApi();builder.Services.AddProblemDetails();builder.Services.AddExceptionHandler<TenantExceptionHandler>();
        builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("tenant",context=>RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??context.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
        var app=builder.Build();app.UseExceptionHandler();
        app.Use(async(context,next)=>{context.Response.Headers["X-Content-Type-Options"]="nosniff";context.Response.Headers.CacheControl="no-store";await next();});
        if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseWhen(c=>!c.Request.Path.StartsWithSegments("/health"),branch=>branch.UseHttpsRedirection());}
        app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();if(app.Environment.IsDevelopment())app.MapOpenApi();
        app.MapGet("/health/live",()=>Results.Ok(new{status="healthy",service="Tenant"}));
        app.MapGet("/health/ready",async(TenantDbContext db,CancellationToken ct)=>
        {
            try{await db.BusinessTypes.Select(t=>t.Code).Take(1).ToListAsync(ct);return Results.Ok(new{status="ready"});}
            catch(Exception ex) when(ex is not OperationCanceledException){return Results.StatusCode(503);}
        });
        if(args.Contains("--initialize")){using var scope=app.Services.CreateScope();await TenantSetup.Initialize(scope.ServiceProvider.GetRequiredService<TenantDbContext>());return;}
        await app.RunAsync();
    }
}
public sealed class TenantExceptionHandler(ILogger<TenantExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception error,CancellationToken ct)
    {
        SqlException? sql=null;
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current is SqlException found){sql=found;break;}
        var status=error is TenantRuleException rule?rule.Status:error is DbUpdateConcurrencyException || sql?.Number is 2601 or 2627 or 1205?409:error is IdentityUnavailableException || sql is not null?503:500;
        context.Response.StatusCode=status;
        if(status==500) logger.LogError("Unhandled Tenant request failure: {ExceptionTypes}",string.Join(" -> ",ExceptionTypes(error)));
        await context.Response.WriteAsJsonAsync(new ProblemDetails{Status=status,Title=error is TenantRuleException known?known.Code:status==409?"Concurrent state conflict; reload and retry.":status==503?"A required service is temporarily unavailable.":"The request could not be completed."},ct);return true;
    }
    private static IEnumerable<string> ExceptionTypes(Exception error)
    {for(Exception? current=error;current is not null;current=current.InnerException)yield return current.GetType().Name;}
}
