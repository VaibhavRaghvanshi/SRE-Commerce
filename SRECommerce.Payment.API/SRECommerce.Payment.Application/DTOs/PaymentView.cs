using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public sealed record PaymentView(Guid Id, Guid OrderId, string Currency, decimal Amount, string Method, PaymentStatus Status, decimal Refunded, string Version, string Provider, string ProviderReference, bool Sandbox = true, int DecimalPlaces = 2);
