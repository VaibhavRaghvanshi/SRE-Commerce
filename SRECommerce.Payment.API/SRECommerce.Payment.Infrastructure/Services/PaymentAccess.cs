using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.Infrastructure;
public sealed class PaymentAccess(ModuleAccess access, ModuleScope scope, RemoteAccess remote) : IPaymentAccess
{
    public UserAccess? Actor => scope.User;
    public Task Human(Guid tenant, ActorPermission permission, CancellationToken ct) => access.AuthorizeAsync(tenant, permission, ct);
    public async Task Machine(Guid tenant, bool recovery, CancellationToken ct)
    {
        if (!recovery) await access.PublicAsync(tenant, ct);
        access.MachineAsync(tenant, "Order");
    }
    public async Task Public(Guid tenant, CancellationToken ct) => _ = await access.PublicAsync(tenant, ct);
    public async Task<string> GiftCardCurrency(Guid tenant, CancellationToken ct)
    {
        var state = await remote.PublicTenantAsync(tenant, ct);
        if (state is null || state.TenantId != tenant || !state.Modules.Contains("payments"))
            throw new BusinessFault("PublishedTenantNotFound", 404);
        return state.Currency;
    }
    public void BindWebhook(Guid tenant)
    {
        if (scope.User is not null && scope.TenantId == tenant) return;
        scope.BindMachine(tenant, new("SandboxWebhook", "signed-sandbox-provider", [], []));
    }
}
