using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public interface IPaymentRepository
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<PaymentTransaction?> Payment(Guid order, CancellationToken ct);
    Task<PaymentProviderConfiguration?> Configuration(CancellationToken ct);
    Task<(PaymentTransaction[] Items, int Total)> List(int page, PaymentStatus? status, CancellationToken ct);
    Task<PaymentAttempt[]> Attempts(Guid payment, CancellationToken ct);
    Task<RefundTransaction[]> Refunds(Guid payment, CancellationToken ct);
    Task<RefundTransaction?> Refund(Guid id, CancellationToken ct);
    Task<PaymentWebhook?> Webhook(Guid id, CancellationToken ct);
    void Add<T>(T value) where T : class;
    void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord entity, string version);
    Task Save(string action, Guid resource, PaymentTransaction? changed, CancellationToken ct);
}
