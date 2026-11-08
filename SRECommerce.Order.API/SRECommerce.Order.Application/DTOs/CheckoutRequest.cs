using System.Text.Json.Serialization;
namespace SRECommerce.Order.Application;
public sealed record CheckoutRequest(Guid Id,Guid PreparationId,Guid ShippingQuoteId,string PaymentMethod,OrderContact Contact,
 [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Guid? GiftCardId=null,
 [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] decimal? CounterCashReceived=null);
