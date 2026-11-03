using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record MethodRequest(Guid OperationId,Guid ZoneId,string Code,string Name,string Description,DeliveryKind Kind,string Currency,decimal FlatFee,decimal PerUnitFee,decimal TaxRate,decimal? FreeAbove,decimal MinimumOrder,decimal? MaximumOrder,decimal MaximumUnits,Guid? PickupWarehouseId,bool Active=true,string? Version=null);
