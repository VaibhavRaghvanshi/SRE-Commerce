using System.Data;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class CustomerEnrollment(IdentityDbContext db, IStoreDirectory stores, IIdentityAccounts accounts) : ICustomerEnrollment
{
    public async Task<EnrollmentResult> JoinAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) return new(EnrollmentStatus.StoreNotFound);
        var availability = await stores.CheckAsync(tenantId, ct);
        if (availability != StoreAvailability.Available)
            return new(availability == StoreAvailability.NotFound ? EnrollmentStatus.StoreNotFound : EnrollmentStatus.Unavailable);

        // Serialize against administrative changes and concurrent first-time joins.
        // Never reactivate a revoked membership or replace a merchant-assigned role.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.Memberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, ct);
        if (existing is not null)
            return new(existing.IsActive ? EnrollmentStatus.AlreadyMember : EnrollmentStatus.Blocked, existing.IsActive ? existing.Role : null);
        db.Memberships.Add(new TenantMembership { TenantId = tenantId, UserId = userId, Role = "Customer" });
        await accounts.AuditAsync(userId, tenantId, "membership.customer.join", "success", ct);
        await transaction.CommitAsync(ct);
        return new(EnrollmentStatus.Joined, "Customer");
    }
}
