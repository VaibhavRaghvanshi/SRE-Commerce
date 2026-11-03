using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Application;

public sealed partial class OrderService
{
    private async Task<CheckoutIntent> ReviewIntent(Guid id, CancellationToken ct) =>
        await repository.FreshIntent(id, ct) ?? throw new BusinessFault("CheckoutNotFound", 404);

    public async Task<CheckoutReview[]> Reviews(Guid tenant, int page, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        if (page is < 1 or > 1000) throw new BusinessFault("InvalidOrderQuery", 400);
        return (await repository.Reviews(page, ct)).Select(x =>
            new CheckoutReview(x.Id, x.Phase, x.Attempts, x.LastFault, x.CreatedAt, x.Deadline, Version(x.Version))).ToArray();
    }

    public async Task<CheckoutView> CancelCheckout(Guid tenant, Guid id, CancelCheckoutRequest request, bool guest, string? capability, CancellationToken ct)
    {
        await access.Authorize(tenant, guest, ActorPermission.Customer, ct);
        var reason = OrderRules.Text(request.Reason, 300);
        return await repository.Transaction(async token =>
        {
            var intent = await ReviewIntent(id, token);
            Owner(intent, guest, capability);
            if (intent.CancellationReason is not null && intent.State is (CheckoutState.Compensating or CheckoutState.Cancelled))
            {
                if (intent.CancellationReason != reason) throw new BusinessFault("CheckoutCancellationAlreadyRequested");
                return await CheckoutView(intent, token);
            }
            if (intent.State is not (CheckoutState.AwaitingProof or CheckoutState.Processing) || intent.LeaseUntil > Now)
                throw new BusinessFault("CheckoutCannotBeCancelledNow");
            var order = await repository.Order(id, token);
            if (order is not null)
            {
                if (order.Status != OrderStatus.AwaitingPayment || order.Historical || order.Items.Any(x => x.ShipmentRequested > 0))
                    throw new BusinessFault("CheckoutCannotBeCancelledNow");
                Transition(order, OrderStatus.CancellationPending, reason);
                order.Revision++; order.UpdatedAt = Now;
            }
            intent.State = CheckoutState.Compensating; intent.Phase = SagaPhase.CompensatePayment;
            intent.CancellationReason = reason; ResetCheckoutLease(intent);
            await repository.Commit("order.checkout.cancel-requested", id, null, token);
            return await CheckoutView(intent, token);
        }, ct);
    }

    public async Task<CheckoutView> RecoverCheckout(Guid tenant, Guid id, CheckoutRecoveryRequest request, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        var reason = OrderRules.Text(request.Reason, 300);
        return await repository.Transaction(async token =>
        {
            var intent = await ReviewIntent(id, token);
            if (intent.State != CheckoutState.ManualReview || intent.Phase == SagaPhase.Done || intent.LeaseUntil > Now)
                throw new BusinessFault("CheckoutNotInReview");
            repository.Version(intent, request.Version);
            var order = await repository.Order(id, token);
            if (order is not null && (order.Historical || order.Status != OrderStatus.ManualReview || order.Items.Any(x => x.ShipmentRequested > 0)))
                throw new BusinessFault("CheckoutNeedsSeparateOrderReview");

            if (request.Cancel) intent.CancellationReason ??= reason;
            var compensating = request.Cancel || intent.Phase >= SagaPhase.CompensatePayment || intent.Deadline <= Now;
            if (compensating && intent.Phase < SagaPhase.CompensatePayment) intent.Phase = SagaPhase.CompensatePayment;
            intent.State = compensating ? CheckoutState.Compensating : intent.Phase == SagaPhase.AwaitCartClaim ? CheckoutState.AwaitingProof : CheckoutState.Processing;
            ResetCheckoutLease(intent);
            if (order is not null)
            {
                var status = intent.CancellationReason is null ? OrderStatus.AwaitingPayment : OrderStatus.CancellationPending;
                order.Status = status; order.Revision++; order.UpdatedAt = Now;
                repository.Add(new OrderStatusHistory { TenantId = tenant, OrderId = id, Previous = OrderStatus.ManualReview, Current = status,
                    Reason = reason, CreatedAt = Now });
            }
            repository.AuditCheckoutReview(id, reason);
            await repository.Commit(request.Cancel ? "order.checkout.cancel-after-review" : "order.checkout.retry-after-review", id, null, token);
            return await CheckoutView(intent, token);
        }, ct);
    }

    private void ResetCheckoutLease(CheckoutIntent intent)
    {
        intent.LeaseId = null; intent.LeaseUntil = null; intent.Attempts = 0; intent.LastFault = null; intent.NextAttemptAt = Now;
    }
}
