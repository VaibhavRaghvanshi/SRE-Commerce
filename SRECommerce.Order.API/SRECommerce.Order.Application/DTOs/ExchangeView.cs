using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ExchangeView(Guid Id,Guid OrderId,Guid ReturnId,Guid OriginalVariantId,Guid ReplacementVariantId,decimal Quantity,ExchangeStatus Status,Guid? ReplacementOrderId,string Version);
