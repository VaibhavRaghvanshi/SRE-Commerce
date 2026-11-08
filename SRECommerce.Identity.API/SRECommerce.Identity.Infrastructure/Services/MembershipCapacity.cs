using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SRECommerce.Identity.Application;

namespace SRECommerce.Identity.Infrastructure;

public sealed class MembershipCapacity(IdentityDbContext db, IMembershipPlans plans)
{
    // Every membership writer takes this lock before inspecting or changing the tenant roster.
    public async Task LockAsync(Guid tenantId, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Membership writes require a SQL transaction.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = 6;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @tenantResource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 4000;
            SELECT @result;
            """;
        var resource = command.CreateParameter();
        resource.ParameterName = "@tenantResource";
        resource.DbType = DbType.String;
        resource.Size = 80;
        resource.Value = $"identity.membership:{tenantId:D}";
        command.Parameters.Add(resource);
        if (await command.ExecuteScalarAsync(ct) is not int result || result < 0)
            throw new MembershipWriteConflictException();
    }

    public Task<int> CountAsync(Guid tenantId, CancellationToken ct) =>
        db.Memberships.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId && m.IsActive, ct);

    public async Task<MembershipSeat> CheckAsync(Guid tenantId, CancellationToken ct)
    {
        var plan = await plans.FindAsync(tenantId, ct);
        if (plan is not { Allowed: true, MaxUsers: > 0 }) return MembershipSeat.PlanInactive;
        return await CountAsync(tenantId, ct) < plan.MaxUsers ? MembershipSeat.Available : MembershipSeat.Full;
    }
}
