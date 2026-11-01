namespace SRECommerce.Payment.Application;
public sealed record SandboxWebhook(Guid EventId, Guid PaymentId, string Currency, decimal Amount, string Outcome);
