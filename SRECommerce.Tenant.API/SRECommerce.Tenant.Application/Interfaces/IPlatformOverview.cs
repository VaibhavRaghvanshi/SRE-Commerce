namespace SRECommerce.Tenant.Application;

public interface IPlatformOverview
{
    Task<PlatformOverview> Summary(CancellationToken ct);
    Task<PlatformHealthSnapshot> Health(CancellationToken ct);
}
