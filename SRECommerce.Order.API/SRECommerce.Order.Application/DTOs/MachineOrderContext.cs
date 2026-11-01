using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record MachineOrderContext(Guid Id,Guid TenantId,Guid? UserId,string Currency,decimal Total,string PaymentMethod,OrderStatus Status,PaymentState PaymentState,bool Historical,OrderLineView[] Items,int DecimalPlaces=2);
