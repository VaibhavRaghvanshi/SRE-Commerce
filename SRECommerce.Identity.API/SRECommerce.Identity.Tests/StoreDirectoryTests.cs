using System.Net;
using System.Net.Http.Json;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Infrastructure;
using Xunit;

namespace SRECommerce.Identity.Tests;

public sealed class StoreDirectoryTests
{
    private sealed class Factory(Func<HttpRequestMessage, HttpResponseMessage> response) : IHttpClientFactory
    {
        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(response(request));
            }
        }
        public HttpClient CreateClient(string name) => new(new Handler(response)) { BaseAddress = new Uri("https://tenant.example.test/") };
    }

    [Fact]
    public async Task Enrollment_checks_exact_public_tenant_and_catalog_entitlement_without_forwarding_user_credentials()
    {
        var tenant = Guid.NewGuid();
        var directory = new HttpStoreDirectory(new Factory(request =>
        {
            Assert.Equal("/api/v1/public/tenants/by-id/" + tenant, request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { tenantId = tenant, modules = new[] { "catalog" } }) };
        }));
        Assert.Equal(StoreAvailability.Available, await directory.CheckAsync(tenant, default));
        var mismatched = new HttpStoreDirectory(new Factory(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { tenantId = Guid.NewGuid(), modules = new[] { "catalog" } }) }));
        Assert.Equal(StoreAvailability.NotFound, await mismatched.CheckAsync(tenant, default));
        var disabled = new HttpStoreDirectory(new Factory(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { tenantId = tenant, modules = Array.Empty<string>() }) }));
        Assert.Equal(StoreAvailability.NotFound, await disabled.CheckAsync(tenant, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, StoreAvailability.NotFound)]
    [InlineData(HttpStatusCode.Redirect, StoreAvailability.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, StoreAvailability.Unavailable)]
    public async Task Missing_or_unavailable_directory_never_grants_membership(HttpStatusCode status, StoreAvailability expected)
    {
        var directory = new HttpStoreDirectory(new Factory(_ => new(status)));
        Assert.Equal(expected, await directory.CheckAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Malformed_directory_document_is_unavailable()
    {
        var directory = new HttpStoreDirectory(new Factory(_ => new(HttpStatusCode.OK) { Content = new StringContent("not-json", System.Text.Encoding.UTF8, "application/json") }));
        Assert.Equal(StoreAvailability.Unavailable, await directory.CheckAsync(Guid.NewGuid(), default));
    }
}
