using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public interface IShippingRepository
{
 Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
 Task<ShippingZone[]> Zones(CancellationToken ct);Task<ShippingMethod[]> Methods(CancellationToken ct);Task<ShippingQuote?> Quote(Guid id,CancellationToken ct);
 Task<Shipment?> Shipment(Guid id,CancellationToken ct);Task<(Shipment[] Items,int Total)> Shipments(int page,ShipmentStatus? status,CancellationToken ct);Task<Shipment[]> OrderShipments(Guid order,CancellationToken ct);Task<TrackingEvent[]> Tracking(Guid shipment,CancellationToken ct);
 Task<ShippingOperation?> Operation(Guid id,CancellationToken ct);Task<ReturnPickup?> Pickup(Guid id,CancellationToken ct);Task<ReturnPickup?> ActivePickup(Guid returnId,CancellationToken ct);Task<ReturnPickup[]> Pickups(CancellationToken ct);
 Task<ShipmentCallback[]> Due(DateTimeOffset now,CancellationToken ct);Task<ShipmentCallback?> FreshCallback(Guid id,CancellationToken ct);
 Task<ShipmentCallback[]> Callbacks(Guid shipment,CancellationToken ct);
 void Add<T>(T value)where T:class;void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord value,string version);Task Save(string action,Guid resource,object? integrationEvent,CancellationToken ct);
}
