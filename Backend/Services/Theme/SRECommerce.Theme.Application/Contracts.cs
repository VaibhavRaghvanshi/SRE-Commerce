using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record CreateTheme([Required,MaxLength(160)]string Name,[MaxLength(40)]string Preset="neutral");
public record SaveTheme([Required]string Version,[Required]ThemeDocument Document);
public record PublishTheme([Required]string Version,string? PublicationVersion);
public record RollbackTheme(Guid ThemeVersionId,[Required]string PublicationVersion);
public record SavePreset(string? Version,[Required]ThemeDocument Document);
public record ThemeView(Guid Id,string Name,bool Archived,string Version,ThemeDocument Document);
public record VersionView(Guid Id,Guid ThemeId,int Revision,string Hash,DateTimeOffset CreatedAt);
public record PublicationView(Guid VersionId,long Sequence,string PublicationVersion,string ThemeVersion);
public record PublicTheme(Guid TenantId,long Sequence,string Source,string BusinessType,ThemeDocument Document,IReadOnlyDictionary<string,string> Variables);
public interface IThemeOperations
{
    Task<IReadOnlyCollection<ThemeView>> List(Guid tenant,int page,CancellationToken ct);
    Task<ThemeView> Create(Guid tenant,CreateTheme request,CancellationToken ct);
    Task<ThemeView> Save(Guid tenant,Guid id,SaveTheme request,CancellationToken ct);
    Task<PublicationView> Publish(Guid tenant,Guid id,PublishTheme request,CancellationToken ct);
    Task<PublicationView> Rollback(Guid tenant,RollbackTheme request,CancellationToken ct);
    Task<IReadOnlyCollection<VersionView>> Versions(Guid tenant,Guid id,CancellationToken ct);
    Task<object> PublicationHistory(Guid tenant,int page,CancellationToken ct);
    Task<ThemeDocument> Version(Guid tenant,Guid id,CancellationToken ct);
    Task<object> Presets(CancellationToken ct);
    Task SavePreset(string businessType,SavePreset request,CancellationToken ct);
    Task<PublicTheme> Public(Guid tenant,string? businessType,string mode,bool mobile,CancellationToken ct);
}
