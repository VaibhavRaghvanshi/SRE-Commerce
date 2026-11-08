using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Application;

public sealed partial class OrderService
{
    public async Task<OrderView> CounterHandover(Guid tenant, Guid id, CounterHandoverRequest request, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Staff, ct);
        if (access.Actor is not { IsPlatformAdmin: false, TenantRole: "Owner" or "Manager" or "Staff" })
            throw new BusinessFault("CounterStaffRequired", 403);
        const string action = "order.counter.handed-over";
        var hash = Hash(request);
        return await repository.Transaction(async token =>
        {
            var order = await Owned(id, false, null, true, token);
            var replay = await Replay(request.OperationId, id, action, hash, token);
            if (replay is not null) return replay;
            repository.Version(order, request.Version);
            if (order.Historical || order.PaymentMethod != "CounterCash" ||
                order.Status != OrderStatus.Confirmed || order.PaymentState != PaymentState.Captured)
                throw new BusinessFault("CounterHandoverUnavailable");

            foreach (var line in order.Items)
                line.ShipmentRequested = line.Fulfilled = line.Delivered = line.Quantity;
            order.DeliveredAt = Now;
            Transition(order, OrderStatus.Fulfilled, "Counter items picked");
            Transition(order, OrderStatus.Delivered, "Handed over at the counter");
            OrderRules.Lines(order);
            return await Record(order, request.OperationId, action, hash, token);
        }, ct);
    }
}
