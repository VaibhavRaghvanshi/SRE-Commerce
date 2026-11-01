using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record CheckoutView(Guid Id,CheckoutState State,SagaPhase Phase,string? Issue,DateTimeOffset CreatedAt,OrderView? Order);
