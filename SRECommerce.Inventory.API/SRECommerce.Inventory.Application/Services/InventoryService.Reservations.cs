using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
    public async Task<ReservationView> Reserve(Guid tenant,ReserveRequest request,bool machine,CancellationToken ct)
    {
        if(machine)await access.Machine(tenant,["Cart","Order","Manufacturing"],ct);else await access.Authorize(tenant,ActorPermission.Manager,ct);
        Id(request.Id);var reference=InventoryRules.Text(request.Reference,120);var hash=Hash(request);
        if(request.Items is null||request.Items.Length is <1 or >100||request.Items.Select(i=>i.VariantId).Distinct().Count()!=request.Items.Length)throw new BusinessFault("InvalidReservationItems",400);
        foreach(var item in request.Items){Id(item.VariantId);InventoryRules.Quantity(item.Quantity);}
        return await repository.Transaction(async token=>
        {
            var previous=await repository.Reservation(request.Id,token);if(previous is not null){if(previous.PayloadHash!=hash)throw new BusinessFault("ReservationKeyReused");return previous.Status==ReservationStatus.Active&&previous.ExpiresAt<=Now?await Finish(tenant,previous,ReservationStatus.Expired,token):View(previous);}
            InventoryRules.Reservation(Now,request.ExpiresAt);
            await WarehouseAvailable(request.WarehouseId,token);var ids=request.Items.Select(i=>i.VariantId).Order().ToArray();var items=await repository.Items(token);if(ids.Any(id=>!items.Any(i=>i.Id==id&&i.Active)))throw new BusinessFault("StockItemUnavailable");
            var balances=await repository.Stock(request.WarehouseId,ids,token);var reservation=new StockReservation{TenantId=tenant,Id=request.Id,WarehouseId=request.WarehouseId,Reference=reference,PayloadHash=hash,ExpiresAt=request.ExpiresAt,CreatedAt=Now};
            foreach(var item in request.Items.OrderBy(i=>i.VariantId))
            {
                var remaining=item.Quantity;
                foreach(var stock in balances.Where(s=>s.Balance.VariantId==item.VariantId&&(s.Batch.ExpiresAt is null||s.Batch.ExpiresAt>request.ExpiresAt)).OrderBy(s=>s.Batch.ExpiresAt??DateTimeOffset.MaxValue).ThenBy(s=>s.Batch.ReceivedAt).ThenBy(s=>s.Balance.Id))
                {
                    var quantity=Math.Min(remaining,stock.Balance.OnHand-stock.Balance.Reserved);if(quantity<=0)continue;
                    Move(tenant,request.Id,stock.Balance,StockAction.Reservation,0,quantity,0,reference);reservation.Lines.Add(new ReservationLine{TenantId=tenant,ReservationId=request.Id,BalanceId=stock.Balance.Id,VariantId=item.VariantId,Quantity=quantity});remaining-=quantity;if(remaining==0)break;
                }
                if(remaining>0)throw new BusinessFault("InsufficientAvailableStock");
            }
            repository.Add(reservation);await repository.Commit("inventory.reserved",request.Id,new{ReservationId=request.Id,request.WarehouseId,request.ExpiresAt,request.Items},token);return View(reservation);
        },ct);
    }
    private async Task<ReservationView> Finish(Guid tenant,StockReservation reservation,ReservationStatus desired,CancellationToken ct)
    {
        var stock=await repository.Stock(reservation.WarehouseId,reservation.Lines.Select(l=>l.VariantId).Distinct().ToArray(),ct);
        foreach(var line in reservation.Lines.OrderBy(l=>l.BalanceId))
        {
            var balance=stock.SingleOrDefault(s=>s.Balance.Id==line.BalanceId)?.Balance??throw new BusinessFault("ReservedBalanceUnavailable");
            Move(tenant,reservation.Id,balance,desired==ReservationStatus.Committed?StockAction.Commit:desired==ReservationStatus.Expired?StockAction.Expire:StockAction.Release,desired==ReservationStatus.Committed?-line.Quantity:0,-line.Quantity,0,reservation.Reference);
        }
        reservation.Status=desired;await repository.Commit("inventory.reservation."+desired.ToString().ToLowerInvariant(),reservation.Id,new{ReservationId=reservation.Id,Status=desired},ct);return View(reservation);
    }
    public async Task<ReservationView> Close(Guid tenant,Guid id,bool commit,CloseReservationRequest request,bool machine,CancellationToken ct)
    {
        if(machine)await access.Machine(tenant,["Cart","Order","Manufacturing"],ct);else await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var value=await repository.Reservation(id,token)??throw new BusinessFault("ReservationNotFound",404);var desired=commit?ReservationStatus.Committed:ReservationStatus.Released;
            if(value.Status==desired||value.Status==ReservationStatus.Expired)return View(value);if(value.Status!=ReservationStatus.Active)throw new BusinessFault("ReservationAlreadyFinal");repository.Version(value,request.Version);
            return await Finish(tenant,value,value.ExpiresAt<=Now?ReservationStatus.Expired:desired,token);
        },ct);
    }
    public async Task<ReservationView> Reservation(Guid tenant,Guid id,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return View(await repository.Reservation(id,ct)??throw new BusinessFault("ReservationNotFound",404));}
    public async Task<int> Expire(Guid tenant,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);return await ExpireBound(ct);}
    public async Task<int> ExpireBound(CancellationToken ct)
    {
        return await repository.Transaction(async token=>
        {var due=await repository.Expired(Now,100,token);foreach(var value in due)await Finish(value.TenantId,value,ReservationStatus.Expired,token);return due.Count;},ct);
    }
}
