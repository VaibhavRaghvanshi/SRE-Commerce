using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.Infrastructure;
public sealed class PaymentAccess(ModuleAccess access, ModuleScope scope) : IPaymentAccess
{
    public UserAccess? Actor => scope.User;
    public Task Human(Guid tenant, ActorPermission permission, CancellationToken ct) => access.AuthorizeAsync(tenant, permission, ct);
    public async Task Machine(Guid tenant, bool recovery, CancellationToken ct)
    {
        if (!recovery) await access.PublicAsync(tenant, ct);
        access.MachineAsync(tenant, "Order");
    }
    public async Task Public(Guid tenant, CancellationToken ct) => _ = await access.PublicAsync(tenant, ct);
    public void BindWebhook(Guid tenant)
    {
        if (scope.User is not null && scope.TenantId == tenant) return;
        scope.BindMachine(tenant, new("SandboxWebhook", "signed-sandbox-provider", [], []));
    }
}
