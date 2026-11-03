using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public interface IShippingService
{
 Task<object> Regions(Guid tenant,CancellationToken ct);
 Task<object> Lookups(Guid tenant,CancellationToken ct);Task<ZoneView> Zone(Guid tenant,Guid? id,ZoneRequest request,CancellationToken ct);Task<MethodView> Method(Guid tenant,Guid? id,MethodRequest request,CancellationToken ct);
 Task<ShippingOption[]> Options(Guid tenant,QuoteOptionsRequest request,bool guest,string? cartKey,CancellationToken ct);Task<ShippingQuoteContract> Quote(Guid tenant,Guid id,CancellationToken ct);
 Task<ShipmentView> CreateShipment(Guid tenant,Guid id,CancellationToken ct);Task<object> Shipments(Guid tenant,int page,ShipmentStatus? status,CancellationToken ct);Task<object> History(Guid tenant,Guid id,CancellationToken ct);Task<ShipmentView> Change(Guid tenant,Guid id,ShipmentStateRequest request,CancellationToken ct);
 Task<object> CustomerTracking(Guid tenant,Guid order,bool guest,string? orderKey,CancellationToken ct);
 Task<ReturnPickupView> Pickup(Guid tenant,ReturnPickupRequest request,CancellationToken ct);Task<ReturnPickupView> ChangePickup(Guid tenant,Guid id,ReturnPickupStateRequest request,CancellationToken ct);Task<ReturnPickupView[]> Pickups(Guid tenant,CancellationToken ct);
 Task<object> Recovery(Guid tenant,Guid id,CancellationToken ct);
}
