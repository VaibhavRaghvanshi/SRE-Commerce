using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record CheckoutRequest(Guid Id,Guid PreparationId,Guid ShippingQuoteId,string PaymentMethod,OrderContact Contact);
