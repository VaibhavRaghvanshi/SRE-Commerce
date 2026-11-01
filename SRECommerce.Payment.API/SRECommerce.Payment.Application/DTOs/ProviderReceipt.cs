using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Application;
public sealed record ProviderReceipt(PaymentStatus Status, string Reference);
