using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using SRECommerce.BuildingBlocks.Infrastructure;
using Xunit;

namespace SRECommerce.Catalog.Tests;

public sealed class DependencyHttpTests
{
    private static ServiceProvider Provider(string environment,bool enabled,HttpMessageHandler? handler=null)
    {
        var services=new ServiceCollection();services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new EnvironmentStub{EnvironmentName=environment});
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"LocalReview:DependencyTimeouts",enabled.ToString()}}).Build());
        var client=services.AddHttpClient("probe",http=>http.BaseAddress=new Uri("http://localhost/"));
        if(handler is not null)client.ConfigurePrimaryHttpMessageHandler(()=>handler);
        client.WithDependencyResilience();return services.BuildServiceProvider();
    }
    [Theory]
    [InlineData("Production",true,2,5)]
    [InlineData("Development",false,2,5)]
    [InlineData("Development",true,8,12)]
    public void Review_budget_requires_both_development_and_explicit_opt_in(string environment,bool enabled,int attempt,int total)
    {
        using var provider=Provider(environment,enabled);
        var options=provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("probe-standard");
        Assert.Equal(TimeSpan.FromSeconds(attempt),options.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(total),options.TotalRequestTimeout.Timeout);
    }
    [Theory]
    [InlineData("POST")][InlineData("PUT")][InlineData("PATCH")][InlineData("DELETE")][InlineData("CONNECT")]
    public async Task Review_profile_does_not_replay_unsafe_requests(string method)
    {
        var handler=new ProbeHandler();using var provider=Provider("Development",true,handler);
        using var response=await provider.GetRequiredService<IHttpClientFactory>().CreateClient("probe").SendAsync(new HttpRequestMessage(new HttpMethod(method),"operation"));
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.Equal(1,handler.Calls);
    }
    [Fact]
    public async Task A_timed_out_post_is_not_replayed()
    {
        var handler=new TimeoutHandler();using var provider=Provider("Development",false,handler);
        var client=provider.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
        var error=await Assert.ThrowsAnyAsync<Exception>(()=>client.PostAsync("operation",new StringContent("{}")));
        Assert.Equal("Polly.Timeout.TimeoutRejectedException",error.GetType().FullName);
        Assert.Equal(1,handler.Calls);
    }
    [Fact]
    public async Task Safe_reads_retain_the_single_bounded_retry()
    {
        var handler=new ProbeHandler{Recover=true};using var provider=Provider("Development",true,handler);
        using var response=await provider.GetRequiredService<IHttpClientFactory>().CreateClient("probe").GetAsync("snapshot");
        Assert.Equal(System.Net.HttpStatusCode.OK,response.StatusCode);Assert.Equal(2,handler.Calls);
    }
    private sealed class ProbeHandler:HttpMessageHandler
    {
        public int Calls;public bool Recover;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;return Task.FromResult(new HttpResponseMessage(Recover&&Calls>1?System.Net.HttpStatusCode.OK:System.Net.HttpStatusCode.ServiceUnavailable));}
    }
    private sealed class EnvironmentStub:IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Production";public string ApplicationName{get;set;}="DependencyHttpTests";
        public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class TimeoutHandler:HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;await Task.Delay(Timeout.InfiniteTimeSpan,ct);return new HttpResponseMessage(System.Net.HttpStatusCode.OK);}
    }
}
