using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Application;
using SRECommerce.Payment.Infrastructure;
namespace SRECommerce.Payment.API;
public partial class PaymentProgram
{
    public static Task Main(string[] args) => ModuleHost.RunAsync<PaymentDbContext>(args, ServiceManifest.Get("Payment"), typeof(PaymentProgram).Assembly, services =>
    {
        services.AddSingleton(TimeProvider.System); services.AddServiceIdentity();
        services.AddScoped<IPaymentRepository, PaymentRepository>(); services.AddScoped<IPaymentAccess, PaymentAccess>();
        services.AddScoped<IOrderPaymentProofs, OrderPaymentProofs>(); services.AddSingleton<IPaymentProvider, SandboxPaymentProvider>();
        services.AddSingleton<IPaymentSignatures, PaymentSignatures>(); services.AddScoped<IPaymentService, PaymentService>();
        services.AddHttpClient("order", (p, client) => { client.BaseAddress = RemoteAccess.BaseUri(p.GetRequiredService<IConfiguration>(), "Order:BaseUrl", "http://localhost:5089/", p.GetRequiredService<IHostEnvironment>().IsDevelopment()); client.Timeout = TimeSpan.FromSeconds(6); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).WithDependencyResilience();
    });
}
