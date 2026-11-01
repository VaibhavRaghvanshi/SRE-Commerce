using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record HistoricalOrderRow(string Reference,Guid? UserId,string Number,OrderStatus Status,PaymentState PaymentState,string PaymentMethod,string Currency,int DecimalPlaces,OrderContact Contact,DateTimeOffset CreatedAt,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,OrderLineView[] Items,decimal ShippingTotal=0,HistoricalSourceInfo? Source=null);
