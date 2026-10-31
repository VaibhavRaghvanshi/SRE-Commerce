using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
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
