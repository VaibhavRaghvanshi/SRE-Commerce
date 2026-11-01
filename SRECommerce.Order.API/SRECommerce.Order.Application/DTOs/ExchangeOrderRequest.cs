using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ExchangeOrderRequest(Guid Id,string Version,Guid OriginalVariantId,Guid ReplacementVariantId,decimal Quantity,string Reason);
