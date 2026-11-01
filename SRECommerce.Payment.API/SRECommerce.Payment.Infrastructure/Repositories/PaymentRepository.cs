using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Application;
using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.Infrastructure;
public sealed class PaymentRepository(PaymentDbContext db, ModuleScope scope, IHttpContextAccessor contexts, ServiceSpec service) : IPaymentRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action(ct); await transaction.CommitAsync(ct); return result;
    }
    public Task<PaymentTransaction?> Payment(Guid order, CancellationToken ct) => db.Payments.SingleOrDefaultAsync(p => p.OrderId == order, ct);
    public Task<PaymentProviderConfiguration?> Configuration(CancellationToken ct) => db.Configurations.SingleOrDefaultAsync(ct);
    public Task<RefundTransaction?> Refund(Guid id, CancellationToken ct) => db.Refunds.SingleOrDefaultAsync(r => r.Id == id, ct);
    public Task<PaymentWebhook?> Webhook(Guid id, CancellationToken ct) => db.Webhooks.SingleOrDefaultAsync(w => w.Id == id, ct);
    public Task<PaymentAttempt[]> Attempts(Guid payment, CancellationToken ct) => db.Attempts.AsNoTracking().Where(a => a.PaymentId == payment).OrderBy(a => a.Sequence).ToArrayAsync(ct);
    public Task<RefundTransaction[]> Refunds(Guid payment, CancellationToken ct) => db.Refunds.AsNoTracking().Where(a => a.PaymentId == payment).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToArrayAsync(ct);
    public async Task<(PaymentTransaction[] Items, int Total)> List(int page, PaymentStatus? status, CancellationToken ct)
    {
        var query = db.Payments.AsQueryable(); if (status is { } filter) query = query.Where(p => p.Status == filter);
        return (await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct), await query.CountAsync(ct));
    }
    public void Add<T>(T value) where T : class => db.Add(value);
    public void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord entity, string version) => db.ExpectedVersion(entity, version);
    public async Task Save(string action, Guid resource, PaymentTransaction? changed, CancellationToken ct)
    {
        db.Record(scope, contexts, action, resource.ToString());
        if (changed is not null) db.Emit(scope, service, contexts, action + ".v1", new { PaymentId = changed.Id, changed.OrderId, changed.Status, changed.Currency, changed.Amount, changed.Refunded });
        await db.SaveChangesAsync(ct);
    }
}
