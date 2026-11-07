namespace SRECommerce.Identity.Application;

public enum StoreAvailability { Available, NotFound, Unavailable }
public interface IStoreDirectory
{
    Task<StoreAvailability> CheckAsync(Guid tenantId, CancellationToken ct);
}
public enum EnrollmentStatus { Joined, AlreadyMember, Blocked, StoreNotFound, Unavailable }
public sealed record EnrollmentResult(EnrollmentStatus Status, string? Role = null);
public interface ICustomerEnrollment
{
    Task<EnrollmentResult> JoinAsync(Guid userId, Guid tenantId, CancellationToken ct);
}
