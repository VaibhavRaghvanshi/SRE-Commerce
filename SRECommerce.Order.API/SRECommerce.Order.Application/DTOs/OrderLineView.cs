using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderLineView(Guid ProductId,Guid VariantId,string Sku,string Name,decimal Quantity,decimal Fulfilled,decimal Delivered,decimal ReturnRequested,decimal Returned,decimal UnitPrice,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,decimal ShipmentRequested=0,decimal RefundAllocated=0);
