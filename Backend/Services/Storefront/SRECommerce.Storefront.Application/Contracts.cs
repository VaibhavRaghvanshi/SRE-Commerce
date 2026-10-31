using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record CreatePage([Required,MaxLength(80)]string Slug,[Required,MaxLength(160)]string Title,[MaxLength(40)]string Template="neutral");
public record SavePage([Required]string Version,[Required,MaxLength(160)]string Title,[Required]LayoutDocument Document);
public record PublishPage([Required]string Version,string? PublicationVersion);
public record RollbackPage(Guid LayoutVersionId,[Required]string PublicationVersion);
public record SaveTemplate(string? Version,[Required]LayoutDocument Document);
public record PageView(Guid Id,string Slug,string Title,string Version,LayoutDocument Document);
public record VersionView(Guid Id,Guid PageId,int Revision,string Hash,DateTimeOffset CreatedAt);
public record PublicationView(Guid VersionId,long Sequence,string PublicationVersion,string PageVersion);
public record PublicLayout(Guid TenantId,string Slug,long Sequence,string Source,string BusinessType,LayoutDocument Document);
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
