using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;

public sealed record CheckoutReview(Guid Id, SagaPhase Phase, int Attempts, string? Issue, DateTimeOffset CreatedAt, DateTimeOffset Deadline, string Version);
public sealed record CheckoutRecoveryRequest(string Version, bool Cancel, string Reason);
public sealed record CancelCheckoutRequest(string Reason);
