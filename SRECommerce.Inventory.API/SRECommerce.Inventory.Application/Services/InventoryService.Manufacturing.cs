using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;

namespace SRECommerce.Inventory.Application;

public sealed partial class InventoryService
{
    public async Task<BalanceView> ReceiveProduction(Guid tenant, ManufacturingReceipt request, CancellationToken ct)
    {
        await access.Machine(tenant, ["Manufacturing"], ct);
        Id(request.OperationId); Id(request.ProductionId); Id(request.WarehouseId); Id(request.VariantId); InventoryRules.Quantity(request.Quantity);
        if (request.OperationId != request.ProductionId) throw new BusinessFault("ProductionReceiptMustUseProductionId", 400);
        var hash = Hash(new { Action = "manufacturing-output", request });
        return await repository.Transaction(async token =>
        {
            if (await repository.Operation(request.OperationId, token) is {} previous)
            {
                if (previous.Action != StockAction.Receipt || previous.PayloadHash != hash) throw new BusinessFault("OperationKeyReused");
                return ContractJson.Read<BalanceView>(previous.Result);
            }
            var reservation = await repository.Reservation(request.ProductionId, token) ?? throw new BusinessFault("ProductionInputsNotConsumed");
            ReservationOwner(reservation, true);
            if (reservation.Status != ReservationStatus.Committed || reservation.WarehouseId != request.WarehouseId ||
                reservation.Lines.Any(line => line.VariantId == request.VariantId))
                throw new BusinessFault("ProductionInputsNotConsumed");
            var lot = "PROD-" + request.ProductionId.ToString("N").ToUpperInvariant();
            await WarehouseAvailable(request.WarehouseId, token); await ItemAvailable(request.VariantId, token);
            if ((await repository.Stock(request.WarehouseId, [request.VariantId], token)).Any(x => x.Batch.Lot == lot))
                throw new BusinessFault("ProductionLotAlreadyExists");
            var stock = await ReceiveStock(tenant, request.WarehouseId, request.VariantId, lot, request.ExpiresAt, request.Quantity,
                request.OperationId, "Manufacturing output " + request.ProductionId.ToString("N"), token);
            await repository.Commit("inventory.production.received", request.ProductionId, new { request.ProductionId, request.VariantId, request.WarehouseId, request.Quantity }, token);
            var result = View(stock);
            repository.Add(new StockOperation { TenantId = tenant, Id = request.OperationId, Action = StockAction.Receipt, PayloadHash = hash, CreatedAt = Now, Result = ContractJson.Write(result) });
            await repository.Commit("inventory.operation.recorded", request.OperationId, null, token);
            return result;
        }, ct);
    }
}
