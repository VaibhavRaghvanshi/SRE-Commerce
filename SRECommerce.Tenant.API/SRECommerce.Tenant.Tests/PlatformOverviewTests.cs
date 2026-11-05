using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;
using SRECommerce.Tenant.Infrastructure;
using Xunit;

namespace SRECommerce.Tenant.Tests;

public sealed class PlatformTestCaller : ITenantCaller
{
    public TenantCaller Current { get; } = new(Guid.NewGuid(), "Platform", true, null, null);
}
public sealed class PlatformProbeHandler : HttpMessageHandler
{
    public ConcurrentBag<string> Hosts { get; } = [];
    private int inFlight; public int Maximum;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("/health/ready", request.RequestUri!.AbsolutePath); Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie"));
        Hosts.Add(request.RequestUri.Host); var current = Interlocked.Increment(ref inFlight);
        int observed; do { observed = Maximum; if (observed >= current) break; } while (Interlocked.CompareExchange(ref Maximum, current, observed) != observed);
        try
        {
            await Task.Delay(25, ct);
            if (request.RequestUri.Host == "discovery") throw new HttpRequestException("Synthetic dependency outage");
            return new HttpResponseMessage(request.RequestUri.Host switch { "order" => HttpStatusCode.ServiceUnavailable, "pricing" => HttpStatusCode.Redirect, _ => HttpStatusCode.OK });
        }
        finally { Interlocked.Decrement(ref inFlight); }
    }
}
public sealed class PlatformOverviewTests(TenantFixture fixture) : IClassFixture<TenantFixture>
{
    [Fact]
    public async Task Overview_is_platform_only_and_never_exposes_an_owner_directory()
    {
        using var anonymous = fixture.Factory.Client(); using var owner = fixture.Factory.Client((await fixture.User()).Tokens.Access);
        foreach (var path in new[] { "/api/v1/platform/overview", "/api/v1/platform/service-health" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
        }
    }
    [Fact]
    public async Task Summary_counts_current_terms_and_UTC_days_and_audits_the_read_without_financial_claims()
    {
        Assert.StartsWith("SRECommerce_TenantTests_", fixture.Factory.DatabaseName);
        await using var db = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(fixture.Factory.Connection).Options, new PlatformTestCaller(), new TenantDataScope());
        var now = DateTimeOffset.UtcNow; var yesterday = new DateTimeOffset(now.UtcDateTime.Date.AddHours(-1), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(14));
        var rows = Enumerable.Range(0, 5).Select(i => new BusinessTenant { OwnerUserId = Guid.NewGuid(), RegistrationRequestId = Guid.NewGuid(), RegistrationFingerprint = Guid.NewGuid().ToString("N"),
            Name = "Synthetic platform test", Slug = "platform-" + Guid.NewGuid().ToString("N"), BusinessTypeCode = "general-store", Status = i == 3 ? TenantStatus.Suspended : TenantStatus.Active, CreatedAt = i == 0 ? yesterday : now }).ToArray();
        db.Tenants.AddRange(rows);
        for (var i = 0; i < rows.Length; i++) db.Subscriptions.Add(new TenantSubscription { TenantId = rows[i].Id, PlanCode = "starter", StartsAt = now.AddDays(-2),
            EndsAt = i == 1 ? now.AddDays(-1) : i == 4 ? now.AddDays(30) : now.AddDays(3), Status = i == 2 ? SubscriptionStatus.Cancelled : SubscriptionStatus.Active });
        db.Modules.Add(new TenantModule { TenantId = rows[0].Id, Module = "catalog", Enabled = true });
        db.Modules.Add(new TenantModule { TenantId = rows[3].Id, Module = "catalog", Enabled = true });
        await db.SaveChangesAsync();
        using var admin = fixture.Factory.Client((await fixture.Admin()).Access);
        using var response = await admin.GetAsync("/api/v1/platform/overview"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<PlatformOverview>())!;
        Assert.Equal(5, summary.TotalTenants); Assert.Equal(2, summary.EffectiveSubscriptions); Assert.Equal(1, summary.ExpiringNextSevenDays);
        Assert.Equal(4, summary.TenantStates.Single(s => s.Status == TenantStatus.Active).Count); Assert.Equal(1, Assert.Single(summary.ConfiguredModules).Count);
        Assert.Equal(30, summary.Registrations.Length); Assert.Equal(1, summary.Registrations.Single(d => d.Date.Date == yesterday.UtcDateTime.Date).Count);
        Assert.All(summary.Registrations, day => Assert.Equal(DateTimeKind.Utc, day.Date.Kind));
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("ownerUserId", json); Assert.DoesNotContain("revenue", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(await db.PlatformAudit.AnyAsync(a => a.Action == "platform.overview.read" && a.Resource == "aggregate"));
    }
    [Fact]
    public async Task Health_checks_are_bounded_header_free_and_distinguish_unconfigured_invalid_and_failed_targets()
    {
        var handler = new PlatformProbeHandler();
        using var child = fixture.Factory.WithWebHostBuilder(builder =>
        {
            foreach (var service in new[] { "Catalog", "Order", "Pricing", "Discovery", "Cart" }) builder.UseSetting("PlatformHealth:Services:" + service, "http://" + service.ToLowerInvariant() + ":8080/");
            builder.UseSetting("PlatformHealth:Services:Shipping", "https://example.test/?secret=invalid");
            builder.UseSetting("PlatformHealth:Services:Manufacturing", "http://example.test/");
            builder.ConfigureServices(services => services.AddHttpClient("platform-health").ConfigurePrimaryHttpMessageHandler(() => handler));
        });
        using var client = child.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await fixture.Admin()).Access);
        using var response = await client.GetAsync("/api/v1/platform/service-health"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = (await response.Content.ReadFromJsonAsync<PlatformHealthSnapshot>())!; Assert.Equal(15, snapshot.Services.Length);
        string Status(string service) => snapshot.Services.Single(row => row.Service == service).Status;
        Assert.Equal("Ready", Status("Catalog")); Assert.Equal("Unavailable", Status("Order")); Assert.Equal("Unavailable", Status("Pricing")); Assert.Equal("Unavailable", Status("Discovery"));
        Assert.Equal("NotConfigured", Status("Identity")); Assert.Equal("InvalidConfiguration", Status("Shipping")); Assert.Equal("InvalidConfiguration", Status("Manufacturing"));
        Assert.Equal(5, handler.Hosts.Count); Assert.InRange(handler.Maximum, 1, 4);
        Assert.DoesNotContain("example.test", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }
}
