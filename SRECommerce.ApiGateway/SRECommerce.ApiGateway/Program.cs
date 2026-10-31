using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SRECommerce.BuildingBlocks.Contracts;
using Yarp.ReverseProxy.Configuration;
namespace SRECommerce.ApiGateway;
public partial class GatewayProgram
{
    public static async Task Main(string[] args)
    {
        if(args.Contains("--healthcheck"))
        {
            try{using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};Environment.ExitCode=(await client.GetAsync(Environment.GetEnvironmentVariable("GATEWAY_HEALTH_URL")??"http://127.0.0.1:8080/health/live")).IsSuccessStatusCode?0:1;}
            catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException){Environment.ExitCode=1;}return;
        }
        var builder=WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=1048576);
        builder.Services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=429;
            o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue("RateLimiting:PermitLimit",300),Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
        builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()??(builder.Environment.IsDevelopment()?["http://localhost:4200"]:[])).AllowAnyHeader().AllowAnyMethod()));
        var routes=new List<RouteConfig>();var clusters=new List<ClusterConfig>();
        foreach(var module in ServiceManifest.All.Where(s=>s.Name is "Identity"or"Tenant"or"Theme"or"Storefront"or"Catalog"or"Inventory"or"Pricing"))
        {
            var key=module.Name.ToLowerInvariant();var configured=builder.Configuration["Services:"+module.Name];
            if(!builder.Environment.IsDevelopment()&&configured is null)throw new InvalidOperationException("Explicit production service URLs are required");
            var address=new Uri(configured??$"http://localhost:{module.Port}/");
            if(!address.IsAbsoluteUri||!string.IsNullOrEmpty(address.UserInfo)||!address.AbsoluteUri.EndsWith('/')||!string.IsNullOrEmpty(address.Query)||!string.IsNullOrEmpty(address.Fragment)||address.Scheme!="https"&&(!builder.Environment.IsDevelopment()||!new[]{"localhost","127.0.0.1",key}.Contains(address.Host)))throw new InvalidOperationException("Invalid service destination");
            clusters.Add(new ClusterConfig{ClusterId=key,Destinations=new Dictionary<string,DestinationConfig>{{"primary",new(){Address=address.AbsoluteUri}}},HttpRequest=new(){ActivityTimeout=TimeSpan.FromSeconds(15)}});
            var families=module.Name switch
            {
                "Identity"=>new[]{"me/{**rest}","accounts/{**rest}","logout","sessions/{**rest}","platform/{**rest}","tenants/{tenant}/memberships/{**rest}"},
                "Tenant"=>["public/{**rest}","tenants/{**rest}","platform/{**rest}","plans","business-types"],
                "Theme"=>["public/{**rest}","tenants/{**rest}","platform/{**rest}","theme-presets"],
                "Storefront"=>["public/{**rest}","tenants/{**rest}","platform/{**rest}","layout-templates"],
                _=>["public/{**rest}","tenants/{**rest}"]
            };
            foreach(var family in families)
            {
                var transforms=new List<IReadOnlyDictionary<string,string>>{new Dictionary<string,string>{{"PathRemovePrefix","/commerce/"+key}},new Dictionary<string,string>{{"PathPrefix","/api/v1"}},new Dictionary<string,string>{{"X-Forwarded","Off"}}};
                foreach(var header in new[]{"Cookie","Proxy-Authorization","X-Tenant-ID","X-User-ID","X-Role","X-User-Role","X-User-Email","Forwarded","X-Forwarded-For","X-Forwarded-Host","X-Forwarded-Proto","X-Forwarded-Prefix"})transforms.Add(new Dictionary<string,string>{{"RequestHeaderRemove",header}});
                routes.Add(new RouteConfig{RouteId=key+"-"+routes.Count,ClusterId=key,Match=new(){Path="/commerce/"+key+"/"+family},Transforms=transforms});
            }
        }
        builder.Services.AddReverseProxy().LoadFromMemory(routes,clusters);
        var app=builder.Build();
        app.Use(async(context,next)=>
        {
            var correlation=context.Request.Headers["X-Correlation-ID"].ToString();if(correlation.Length is <1 or >100||correlation.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))correlation=Guid.NewGuid().ToString("N");
            context.TraceIdentifier=correlation;context.Request.Headers["X-Correlation-ID"]=correlation;context.Response.Headers["X-Correlation-ID"]=correlation;context.Response.Headers["X-Content-Type-Options"]="nosniff";
            var path=context.Request.Path.Value??"";
            if(path.Split('/').Any(s=>s.Equals("internal",StringComparison.OrdinalIgnoreCase))||path.Contains("%",StringComparison.Ordinal)||path.Contains("..",StringComparison.Ordinal)){context.Response.StatusCode=404;return;}
            await next();
        });
        if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseHttpsRedirection();}
        app.UseCors();app.UseRateLimiter();app.MapGet("/health/live",()=>Results.Ok(new{status="healthy",service="ApiGateway"}));
        app.MapReverseProxy();await app.RunAsync();
    }
}
