namespace SRECommerce.Shipping.Application;
public interface IShippingDependencies
{
 Task<PreparedShippingCart> Preparation(Guid tenant,Guid id,Guid? user,string? role,string? cartKey,CancellationToken ct);
 Task<WarehouseProof> Warehouse(Guid tenant,Guid id,CancellationToken ct);
 Task<WarehouseProof[]> Warehouses(Guid tenant,Guid[] ids,CancellationToken ct);
 Task<ShipmentContextProof> Fulfillment(Guid tenant,Guid id,CancellationToken ct);
 Task UpdateFulfillment(Guid tenant,Guid fulfillment,OrderShipmentUpdate update,CancellationToken ct);
 Task<ReturnPickupContext> Return(Guid tenant,Guid id,CancellationToken ct);
 Task VerifyCustomerOrder(Guid tenant,Guid id,bool guest,string? orderKey,CancellationToken ct);
}
