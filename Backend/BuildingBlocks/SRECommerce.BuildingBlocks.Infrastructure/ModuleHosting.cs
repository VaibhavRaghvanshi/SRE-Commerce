using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public interface IModuleInitializer {Task InitializeAsync(CancellationToken ct);}
public static class ModuleDatabase
{
    public static string Check(ServiceSpec module,string connection,bool development)
    {
        var sql=new SqlConnectionStringBuilder(connection);
        if(sql.InitialCatalog!=module.Database&&!sql.InitialCatalog.StartsWith(module.TestPrefix,StringComparison.Ordinal))throw new InvalidOperationException("Refusing database outside the service namespace");
        if(!development&&sql.TrustServerCertificate)throw new InvalidOperationException("Production SQL certificate validation is required");
        return connection;
    }
    public static string DesignConnection(ServiceSpec module)
    {var connection=Environment.GetEnvironmentVariable(module.Name.ToUpperInvariant()+"_MIGRATION_CONNECTION")??$"Server=localhost,1433;Database={module.Database};Integrated Security=true;Encrypt=true;TrustServerCertificate=true";return Check(module,connection,true);}
}
public static class ModuleHost
{
    public static async Task RunAsync<TContext>(string[] args,ServiceSpec module,Assembly api,Action<IServiceCollection> register,Action<WebApplication>? configure=null)where TContext:DbContext,IModuleStore
    {
        if(args.Contains("--healthcheck"))
        {
            try{using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Environment.ExitCode=(await client.GetAsync(Environment.GetEnvironmentVariable("MODULE_HEALTH_URL")??"http://127.0.0.1:8080/health/ready")).IsSuccessStatusCode?0:1;}
            catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){Environment.ExitCode=1;}return;
        }
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=args.Where(x=>x!="--initialize").ToArray(),ApplicationName=api.GetName().Name});
        var development=builder.Environment.IsDevelopment();
        var connection=builder.Configuration.GetConnectionString(module.Name)??(development?$"Server=localhost,1433;Database={module.Database};Integrated Security=true;Encrypt=true;TrustServerCertificate=true":throw new InvalidOperationException("Service SQL connection required"));
        ModuleDatabase.Check(module,connection,development);
        builder.Services.AddSingleton(module);builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ModuleScope>();
        builder.Services.AddDbContext<TContext>(o=>o.UseSqlServer(connection));builder.Services.AddScoped<IModuleStore>(p=>p.GetRequiredService<TContext>());
        builder.Services.AddScoped<RemoteAccess>();builder.Services.AddScoped<ModuleAccess>();
        builder.Services.AddSingleton<IPublishedSnapshots,PublishedSnapshots>();
        builder.Services.AddHttpClient("identity",c=>{c.BaseAddress=RemoteAccess.BaseUri(builder.Configuration,"Identity:BaseUrl",development?"http://localhost:5081/":"https://localhost:7081/",development);c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
        builder.Services.AddHttpClient("tenant",c=>{c.BaseAddress=RemoteAccess.BaseUri(builder.Configuration,"Tenant:BaseUrl",development?"http://localhost:5082/":"https://localhost:7082/",development);c.Timeout=TimeSpan.FromSeconds(6);}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
        builder.Services.AddAuthentication("CommerceContext").AddScheme<AuthenticationSchemeOptions,CommerceContextHandler>("CommerceContext",_=>{});
        builder.Services.AddAuthorization(o=>o.AddPolicy("Machine",p=>p.RequireAuthenticatedUser().RequireClaim("actor_type","service")));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddControllers().AddJsonOptions(o=>o.JsonSerializerOptions.UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddOpenApi();builder.Services.AddProblemDetails();builder.Services.AddExceptionHandler<ModuleExceptionHandler>();
        builder.Services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=429;
            o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("RateLimiting:PermitLimit",300),Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
        register(builder.Services);
        var app=builder.Build();app.UseExceptionHandler();
        app.Use(async(context,next)=>
        {
            var supplied=context.Request.Headers["X-Correlation-ID"].ToString();context.TraceIdentifier=supplied.Length is >0 and <=100&&supplied.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-')?supplied:Guid.NewGuid().ToString("N");
            context.Response.Headers["X-Correlation-ID"]=context.TraceIdentifier;context.Response.Headers["X-Content-Type-Options"]="nosniff";context.Response.Headers.CacheControl="no-store";
            await next();
        });
        if(!development){app.UseHsts();app.UseWhen(c=>!c.Request.Path.StartsWithSegments("/health"),branch=>branch.UseHttpsRedirection());}
        app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllers();if(development)app.MapOpenApi();
        app.MapGet("/health/live",(RequestDelegate)(async context=>await context.Response.WriteAsJsonAsync(new{status="healthy",service=module.Name},context.RequestAborted)));
        app.MapGet("/health/ready",(RequestDelegate)(async context=>
        {
            var db=context.RequestServices.GetRequiredService<TContext>();
            try{await db.Outbox.IgnoreQueryFilters().OrderBy(x=>x.Id).Select(x=>x.Id).Take(1).ToListAsync(context.RequestAborted);await context.Response.WriteAsJsonAsync(new{status="ready",service=module.Name},context.RequestAborted);}
            catch(Exception ex)when(ex is not OperationCanceledException){context.Response.StatusCode=503;}
        }));
        configure?.Invoke(app);
        if(args.Contains("--initialize"))
        {
            if(!development)throw new InvalidOperationException("Development catalog initialization is explicit and Development-only");
            using var scope=app.Services.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindBootstrap();
            foreach(var initializer in scope.ServiceProvider.GetServices<IModuleInitializer>())await initializer.InitializeAsync(CancellationToken.None);
            return;
        }
        await app.RunAsync();
    }
}
public sealed class CommerceContextHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,RemoteAccess remote,ServiceSpec module):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(Context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)return AuthenticateResult.NoResult();
        var raw=Request.Headers.Authorization.ToString();if(string.IsNullOrEmpty(raw))return AuthenticateResult.NoResult();
        if(raw.Length>16384||!AuthenticationHeaderValue.TryParse(raw,out var header)||!header.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(header.Parameter))return AuthenticateResult.Fail("Bearer required");
        ClaimsIdentity identity;
        if(Request.Path.StartsWithSegments("/internal/v1"))
        {
            var actor=await remote.MachineAsync(header.Parameter,Context.RequestAborted);
            if(actor is null||actor.Scopes is null||!actor.Scopes.Contains(module.InternalScope)||actor.Audiences is null||!actor.Audiences.Contains("srecommerce.internal")||!ServiceManifest.All.Any(s=>s.Name==actor.Service))return AuthenticateResult.Fail("Scoped service identity required");
            Context.Items[typeof(MachineAccess)]=actor;identity=new([new Claim(ClaimTypes.NameIdentifier,actor.ClientId),new Claim("actor_type","service")],Scheme.Name);
        }
        else
        {
            var actor=await remote.UserAsync(header.Parameter,Context.RequestAborted);
            if(actor is null||actor.UserId==Guid.Empty||actor.Scopes is null||!new[]{"identity_api","tenant_api","commerce_api"}.All(actor.Scopes.Contains)||actor.Audiences is null||!actor.Audiences.Contains("srecommerce.commerce"))return AuthenticateResult.Fail("Current commerce user required");
            Context.Items[typeof(UserAccess)]=actor;identity=new([new Claim(ClaimTypes.NameIdentifier,actor.UserId.ToString()),new Claim("actor_type","user")],Scheme.Name);
        }
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name));
    }
}
public sealed class ModuleExceptionHandler(ILogger<ModuleExceptionHandler> logger):IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception error,CancellationToken ct)
    {
        SqlException? sql=null;for(Exception? current=error;current is not null;current=current.InnerException)if(current is SqlException found){sql=found;break;}
        var status=error is BusinessFault rule?rule.Status:error is Kernel.DomainFault domain?domain.Status:error is DbUpdateConcurrencyException||sql?.Number is 2601 or 2627 or 547 or 1205?409:error is DependencyFault||sql is not null?503:500;
        if(status==500)logger.LogError("Unhandled module failure ({ExceptionType})",error.GetType().Name);
        context.Response.StatusCode=status;await context.Response.WriteAsJsonAsync(new ProblemDetails{Status=status,Title=error is BusinessFault known?known.Code:error is Kernel.DomainFault knownDomain?knownDomain.Code:status==409?"Concurrent state conflict; reload and retry":status==503?"A required dependency is unavailable":"Request could not be completed"},ct);return true;
    }
}
