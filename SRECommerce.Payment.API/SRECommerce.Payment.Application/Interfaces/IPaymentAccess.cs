using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Payment.Application;
public interface IPaymentAccess
{
    UserAccess? Actor { get; }
    Task Human(Guid tenant, ActorPermission permission, CancellationToken ct);
    Task Machine(Guid tenant, bool recovery, CancellationToken ct);
    Task Public(Guid tenant, CancellationToken ct);
    Task<string> GiftCardCurrency(Guid tenant, CancellationToken ct);
    void BindWebhook(Guid tenant);
}
