using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderView(Guid Id,string Number,OrderStatus Status,PaymentState PaymentState,string PaymentMethod,string Currency,int DecimalPlaces,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal ItemsTotal,decimal ShippingTotal,decimal Total,OrderContact Contact,OrderLineView[] Items,string? InvoiceReference,bool Historical,DateTimeOffset CreatedAt,string Version,decimal Refunded=0,DateTimeOffset? DeliveredAt=null);
