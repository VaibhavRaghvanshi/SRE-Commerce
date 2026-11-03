using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Infrastructure;

namespace SRECommerce.Notification.API;

public partial class NotificationProgram
{
    public static Task Main(string[] args) => ModuleHost.RunAsync<NotificationDbContext>(args, ServiceManifest.Get("Notification"), typeof(NotificationProgram).Assembly, services =>
    {
        services.AddServiceIdentity();
        services.AddScoped<INotificationAccess, NotificationAccess>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddScoped<IOrderNotices, OrderNotices>();
        services.AddScoped<NotificationService>();
        services.AddSingleton<INotificationTransport, DevelopmentNotificationTransport>();
        services.AddHostedService<NotificationWorker>();
        services.AddHttpClient("order", (p, c) =>
        {
            c.BaseAddress = RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(), "Order:BaseUrl", "http://localhost:5089/", p.GetRequiredService<IHostEnvironment>().IsDevelopment());
            c.Timeout = TimeSpan.FromSeconds(6);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).WithDependencyResilience();
    });
}
