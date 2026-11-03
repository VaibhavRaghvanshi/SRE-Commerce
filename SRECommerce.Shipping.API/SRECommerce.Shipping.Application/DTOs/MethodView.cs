using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record MethodView(Guid Id,Guid ZoneId,string Code,string Name,string Description,DeliveryKind Kind,string Currency,decimal FlatFee,decimal PerUnitFee,decimal TaxRate,decimal? FreeAbove,decimal MinimumOrder,decimal? MaximumOrder,decimal MaximumUnits,Guid? PickupWarehouseId,bool Active,string Version);
