using SRECommerce.Payment.Application;
using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Infrastructure;
// A local simulation only. Receipts and state are committed atomically in PaymentDB; no payment network is contacted.
public sealed class SandboxPaymentProvider : IPaymentProvider
{
    public string Name => "Sandbox";
    public ProviderReceipt Begin(Guid payment, string method, SandboxOutcome outcome) => new(
        method == "COD" ? PaymentStatus.CashOnDelivery : outcome switch { SandboxOutcome.Authorized => PaymentStatus.Authorized, SandboxOutcome.Pending => PaymentStatus.Pending, _ => PaymentStatus.Failed }, Reference(payment));
    public ProviderReceipt Capture(Guid payment) => new(PaymentStatus.Captured, Reference(payment));
    public string Refund(Guid operation) => "sandbox-refund:" + operation.ToString("N");
    public bool VerifyReference(Guid payment, string reference) => reference == Reference(payment);
    private static string Reference(Guid payment) => "sandbox:" + payment.ToString("N");
}
