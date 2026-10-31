using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SRECommerce.Identity.API;
using Xunit;
namespace SRECommerce.Identity.Tests;
public sealed class BrowserPolicyTests
{
    private sealed class EnvironmentInfo(string name):IHostEnvironment
    {public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="Tests";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    [Fact]
    public void Development_allows_only_explicit_local_browser_callback_origins()
    {var policy=IdentityBrowserPolicy.FormAction(new ConfigurationBuilder().Build(),new EnvironmentInfo("Development"));Assert.Contains("'self'",policy);Assert.Contains("http://localhost:4000",policy);Assert.DoesNotContain("*",policy);Assert.DoesNotContain("https://untrusted.example",policy);}
    [Fact]
    public void Production_requires_https_and_uses_configured_origins()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Identity:FormRedirectOrigins:0","https://store.example"}}).Build();
        Assert.Equal("'self' https://store.example",IdentityBrowserPolicy.FormAction(config,new EnvironmentInfo("Production")));
        config["Identity:FormRedirectOrigins:0"]="http://store.example";Assert.Throws<InvalidOperationException>(()=>IdentityBrowserPolicy.FormAction(config,new EnvironmentInfo("Production")));
    }
}
