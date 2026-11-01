using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public interface IPaymentProvider
{
    string Name { get; }
    ProviderReceipt Begin(Guid payment, string method, SandboxOutcome outcome);
    ProviderReceipt Capture(Guid payment);
    string Refund(Guid operation);
    bool VerifyReference(Guid payment, string reference);
}
