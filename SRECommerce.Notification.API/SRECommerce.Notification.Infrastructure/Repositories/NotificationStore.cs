using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class NotificationStore(NotificationDbContext db, ModuleScope scope, IHttpContextAccessor contexts) : INotificationStore
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action(ct); await transaction.CommitAsync(ct); return result;
    }
    public Task<NotificationTemplate?> Template(NotificationKind kind, NotificationChannel channel, CancellationToken ct) =>
        db.Templates.SingleOrDefaultAsync(x => x.Kind == kind && x.Channel == channel, ct);
    public Task<NotificationTemplate[]> Templates(CancellationToken ct) => db.Templates.AsNoTracking().OrderBy(x => x.Kind).ThenBy(x => x.Channel).ToArrayAsync(ct);
    public Task<NotificationPreference?> Preference(Guid user, CancellationToken ct) => db.Preferences.SingleOrDefaultAsync(x => x.UserId == user, ct);
    public Task<NotificationJob[]> Jobs(int page, CancellationToken ct) => db.Jobs.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.EventId).ThenBy(x => x.Channel).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
    public Task<NotificationJob[]> ForEvent(Guid id, CancellationToken ct) => db.Jobs.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Channel).ToArrayAsync(ct);
    public Task<NotificationJob?> Job(Guid id, NotificationChannel channel, CancellationToken ct) => db.Jobs.SingleOrDefaultAsync(x => x.EventId == id && x.Channel == channel, ct);
    public Task<NotificationDelivery[]> Attempts(Guid id, NotificationChannel channel, CancellationToken ct) =>
        db.Deliveries.AsNoTracking().Where(x => x.EventId == id && x.Channel == channel).OrderBy(x => x.Attempt).ToArrayAsync(ct);
    public Task<InboxReceipt?> Receipt(Guid id, CancellationToken ct) => db.Inbox.AsNoTracking().Where(x => x.Id == id).Select(x => new InboxReceipt(x.PayloadHash)).SingleOrDefaultAsync(ct);
    public void AddReceipt(Guid id, string hash) => db.Inbox.Add(new() { Id = id, TenantId = scope.TenantId!.Value, Producer = "Order", Type = "order.notice.v1", PayloadHash = hash });
    public void Add<T>(T row) where T : class => db.Add(row);
    public void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord row, string version) => db.ExpectedVersion(row, version);
    public async Task Save(string action, string resource, CancellationToken ct)
    { db.Record(scope, contexts, action, resource); await db.SaveChangesAsync(ct); }
}
