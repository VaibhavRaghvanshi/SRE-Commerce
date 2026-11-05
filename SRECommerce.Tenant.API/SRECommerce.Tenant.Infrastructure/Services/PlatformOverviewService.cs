using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;

public sealed class PlatformOverviewService(TenantDbContext db, ITenantCaller caller, IHttpClientFactory clients,
    IConfiguration configuration, IHostEnvironment environment) : IPlatformOverview
{
    private static readonly string[] Services = ["Identity", "Tenant", "Theme", "Storefront", "Catalog", "Inventory", "Pricing",
        "Cart", "Order", "Payment", "Shipping", "Notification", "Manufacturing", "Discovery", "ApiGateway"];
    private void Admin()
    {
        if (!caller.Current.IsPlatformAdmin) throw new TenantRuleException("PlatformAdministratorRequired", 403);
    }
    private async Task Record(string action, string resource, CancellationToken ct)
    {
        db.PlatformAudit.Add(new TenantPlatformAudit { ActorUserId = caller.Current.UserId, Action = action, Resource = resource });
        await db.SaveChangesAsync(ct);
    }
    public async Task<PlatformOverview> Summary(CancellationToken ct)
    {
        Admin(); var now = DateTimeOffset.UtcNow; var first = now.UtcDateTime.Date.AddDays(-29);
        var states = await db.Tenants.AsNoTracking().GroupBy(t => t.Status).Select(g => new TenantStatusCount(g.Key, g.Count())).ToArrayAsync(ct);
        var types = await db.Tenants.AsNoTracking().GroupBy(t => t.BusinessTypeCode).Select(g => new PlatformCount(g.Key, g.Count())).ToArrayAsync(ct);
        var effective = from sub in db.Subscriptions.AsNoTracking() join tenant in db.Tenants.AsNoTracking() on sub.TenantId equals tenant.Id
            where tenant.Status == TenantStatus.Active && (sub.Status == SubscriptionStatus.Trial || sub.Status == SubscriptionStatus.Active)
                && sub.StartsAt <= now && sub.EndsAt > now select sub;
        var active = await effective.CountAsync(ct); var expiring = await effective.CountAsync(s => s.EndsAt <= now.AddDays(7), ct);
        var plans = await db.Plans.CountAsync(p => p.IsActive, ct);
        var modules = await (from module in db.Modules.AsNoTracking() join tenant in db.Tenants.AsNoTracking() on module.TenantId equals tenant.Id
            where tenant.Status == TenantStatus.Active && module.Enabled group module by module.Module into grouped
            select new PlatformCount(grouped.Key, grouped.Count())).ToArrayAsync(ct);
        var registrations = await db.Tenants.AsNoTracking().Where(t => t.CreatedAt >= new DateTimeOffset(first, TimeSpan.Zero) && t.CreatedAt <= now)
            .GroupBy(t => EF.Functions.AtTimeZone(t.CreatedAt, "UTC").Date).Select(g => new { Date = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var days = Enumerable.Range(0, 30).Select(i => first.AddDays(i))
            .Select(day => new RegistrationDay(DateTime.SpecifyKind(day, DateTimeKind.Utc), registrations.SingleOrDefault(r => r.Date == day)?.Count ?? 0)).ToArray();
        await Record("platform.overview.read", "aggregate", ct);
        return new(now, states.Sum(s => s.Count), active, expiring, plans,
            Enum.GetValues<TenantStatus>().Select(status => new TenantStatusCount(status, states.SingleOrDefault(s => s.Status == status)?.Count ?? 0)).ToArray(),
            types.OrderBy(t => t.Code, StringComparer.Ordinal).ToArray(), modules.OrderBy(m => m.Code, StringComparer.Ordinal).ToArray(), days);
    }
    public async Task<PlatformHealthSnapshot> Health(CancellationToken ct)
    {
        Admin(); await Record("platform.health.read", "configured-services", ct);
        var now = DateTimeOffset.UtcNow; using var gate = new SemaphoreSlim(4);
        var results = await Task.WhenAll(Services.Select(async service =>
        {
            var value = configuration["PlatformHealth:Services:" + service];
            if (string.IsNullOrWhiteSpace(value)) return new PlatformServiceHealth(service, "NotConfigured", null);
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http"
                    && (uri.IsLoopback || Services.Any(name => name.Equals(uri.Host, StringComparison.OrdinalIgnoreCase)) || uri.Host is "host.docker.internal" or "api-gateway")))
                return new PlatformServiceHealth(service, "InvalidConfiguration", null);
            await gate.WaitAsync(ct); var watch = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "health/ready"));
                using var http = clients.CreateClient("platform-health");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                return new PlatformServiceHealth(service, response.IsSuccessStatusCode ? "Ready" : "Unavailable", watch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            { return new PlatformServiceHealth(service, "Unavailable", watch.ElapsedMilliseconds); }
            finally { gate.Release(); }
        }));
        return new(now, results);
    }
}
