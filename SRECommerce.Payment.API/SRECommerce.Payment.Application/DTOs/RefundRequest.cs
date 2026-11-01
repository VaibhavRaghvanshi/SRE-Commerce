namespace SRECommerce.Payment.Application;
public sealed record RefundRequest(Guid Id, decimal Amount, string Reason);
