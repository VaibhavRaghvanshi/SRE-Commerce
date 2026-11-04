using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public static class IntegrationInbox
{
    public static async Task Apply(IModuleStore store, EventEnvelope message, Func<CancellationToken, Task> apply, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(message))));
        await using var transaction = await store.Context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = await store.Inbox.IgnoreQueryFilters().SingleOrDefaultAsync(row => row.Id == message.Id, ct);
        if (existing is not null)
        {
            if (existing.TenantId != message.TenantId || existing.Producer != message.Producer ||
                existing.Type != message.Type || existing.PayloadHash != hash)
                throw new BusinessFault("IntegrationEventIdentityReused", 400);
            return;
        }
        await apply(ct);
        store.Inbox.Add(new InboxMessage
        {
            Id = message.Id, TenantId = message.TenantId, Producer = message.Producer,
            Type = message.Type, PayloadHash = hash
        });
        await store.Context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
