namespace SRECommerce.Identity.Application;

public sealed record MembershipPlan(bool Allowed, int MaxUsers);

public interface IMembershipPlans
{
    Task<MembershipPlan?> FindAsync(Guid tenantId, CancellationToken ct);
}

public sealed class MembershipPlanUnavailableException : Exception;
public sealed class MembershipWriteConflictException : Exception;

public enum MembershipSeat { Available, PlanInactive, Full }
