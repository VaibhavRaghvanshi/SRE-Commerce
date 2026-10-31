using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
namespace SRECommerce.ApiGateway.Tests;
public sealed class GatewayFixture:IAsyncLifetime
{
    private WebApplication upstream=null!;
    public GatewayFactory Factory{get;private set;}=null!;
    public async Task InitializeAsync()
    {
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.SetMinimumLevel(LogLevel.Error);upstream=builder.Build();
        upstream.Run(async context=>await context.Response.WriteAsJsonAsync(new{path=context.Request.Path.Value,method=context.Request.Method,tenant=context.Request.Headers["X-Tenant-ID"].ToString(),role=context.Request.Headers["X-Role"].ToString(),forwarded=context.Request.Headers["X-Forwarded-For"].ToString(),correlation=context.Request.Headers["X-Correlation-ID"].ToString(),authorization=context.Request.Headers.ContainsKey("Authorization")}));
        await upstream.StartAsync();var address=upstream.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()+"/";Factory=new(address);
    }
    public async Task DisposeAsync(){Factory.Dispose();await upstream.DisposeAsync();}
}
public sealed class GatewayFactory(string address):WebApplicationFactory<GatewayProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {builder.UseEnvironment("Development");foreach(var name in new[]{"Identity","Tenant","Theme","Storefront","Catalog","Inventory"})builder.UseSetting("Services:"+name,address);builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));}
}
public sealed class GatewayTests(GatewayFixture fixture):IClassFixture<GatewayFixture>
{
    [Fact]
    public async Task Allowed_route_is_rewritten_and_spoofed_context_headers_are_removed()
    {
        using var client=fixture.Factory.CreateClient();using var request=new HttpRequestMessage(HttpMethod.Get,"/commerce/catalog/public/tenants/abc/catalog/products?search=spice");
        request.Headers.Add("X-Tenant-ID","spoofed");request.Headers.Add("X-Role","SuperAdmin");request.Headers.Add("X-Forwarded-For","10.0.0.1");request.Headers.Add("X-Correlation-ID","test-correlation");request.Headers.Authorization=new("Bearer","test-value");
        var response=await client.SendAsync(request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var body=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("/api/v1/public/tenants/abc/catalog/products",body.GetProperty("path").GetString());Assert.Equal("",body.GetProperty("tenant").GetString());Assert.Equal("",body.GetProperty("role").GetString());Assert.Equal("",body.GetProperty("forwarded").GetString());Assert.True(body.GetProperty("authorization").GetBoolean());Assert.Equal("test-correlation",body.GetProperty("correlation").GetString());
    }
    [Theory][InlineData("/internal/v1/access")][InlineData("/commerce/catalog/internal/v1/variants/1")][InlineData("/commerce/catalog/health/ready")][InlineData("/commerce/not-a-service/public/x")][InlineData("/commerce/catalog/public/internal/x")]
    public async Task Internal_health_and_unknown_routes_are_not_exposed(string path)
    {using var client=fixture.Factory.CreateClient();Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path)).StatusCode);}
    [Fact]
    public async Task Business_post_preserves_method_and_health_works()
    {using var client=fixture.Factory.CreateClient();var result=await client.PostAsJsonAsync("/commerce/catalog/tenants/id/catalog/products",new{value="sample"});Assert.Equal(HttpStatusCode.OK,result.StatusCode);Assert.Equal("POST",(await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("method").GetString());Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/live")).StatusCode);}
    [Fact]
    public async Task Cors_uses_explicit_origin_allowlist()
    {using var client=fixture.Factory.CreateClient();using var request=new HttpRequestMessage(HttpMethod.Get,"/health/live");request.Headers.Add("Origin","https://untrusted.example");var response=await client.SendAsync(request);Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));}
}
