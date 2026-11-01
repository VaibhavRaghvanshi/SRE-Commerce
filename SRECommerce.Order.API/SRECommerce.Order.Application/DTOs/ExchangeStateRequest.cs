using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ExchangeStateRequest(Guid OperationId,string Version,ExchangeStatus Status,Guid? ReplacementOrderId=null);
