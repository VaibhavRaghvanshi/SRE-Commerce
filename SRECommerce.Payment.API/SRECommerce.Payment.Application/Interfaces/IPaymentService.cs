using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public interface IPaymentService
{
    Task<PaymentView> Start(Guid tenant, Guid order, CancellationToken ct);
    Task<PaymentView> Read(Guid tenant, Guid order, bool machine, bool customer, CancellationToken ct);
    Task<PaymentView> Capture(Guid tenant, Guid order, string version, CancellationToken ct);
    Task<PaymentView> Abort(Guid tenant, Guid order, CancellationToken ct);
    Task<PaymentView> Refund(Guid tenant, Guid order, RefundRequest request, CancellationToken ct);
    Task<PaymentConfigView> Configuration(Guid tenant, PaymentConfigRequest? request, CancellationToken ct);
    Task<object> Methods(Guid tenant, CancellationToken ct);
    Task<object> List(Guid tenant, int page, PaymentStatus? status, CancellationToken ct);
    Task<object> History(Guid tenant, Guid order, CancellationToken ct);
    Task<object> Reconcile(Guid tenant, Guid order, CancellationToken ct);
    Task<object> Webhook(Guid tenant, string document, string timestamp, string signature, CancellationToken ct);
    Task<object> Decide(Guid tenant, Guid order, SandboxDecisionRequest request, CancellationToken ct);
}
