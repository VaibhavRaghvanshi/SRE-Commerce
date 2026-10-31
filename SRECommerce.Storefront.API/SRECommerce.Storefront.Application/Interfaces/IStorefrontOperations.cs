using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public interface IStorefrontOperations
{
    Task<IReadOnlyCollection<PageView>> List(Guid tenant,int page,CancellationToken ct);
    Task<PageView> Create(Guid tenant,CreatePage request,CancellationToken ct);
    Task<PageView> Save(Guid tenant,Guid id,SavePage request,CancellationToken ct);
    Task<PublicationView> Publish(Guid tenant,Guid id,PublishPage request,CancellationToken ct);
    Task<PublicationView> Rollback(Guid tenant,Guid id,RollbackPage request,CancellationToken ct);
    Task<IReadOnlyCollection<VersionView>> Versions(Guid tenant,Guid id,CancellationToken ct);
    Task<LayoutDocument> Version(Guid tenant,Guid id,Guid version,CancellationToken ct);
    Task<object> History(Guid tenant,Guid id,int page,CancellationToken ct);
    Task<object> Templates(CancellationToken ct);
    Task SaveTemplate(string businessType,SaveTemplate request,CancellationToken ct);
    Task<PublicLayout> Public(Guid tenant,string slug,string? businessType,CancellationToken ct);
}
