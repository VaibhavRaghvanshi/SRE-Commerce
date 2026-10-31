using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Infrastructure;
public sealed class ThemeOperations(ThemeDbContext db,ModuleAccess access,ModuleScope scope,ServiceSpec module,IHttpContextAccessor contexts,IPublishedSnapshots snapshots,RemoteAccess remote,ILogger<ThemeOperations> logger):IThemeOperations
{
    private static ThemeDocument Document(string json){var value=ContractJson.Read<ThemeDocument>(json);ThemeRules.Validate(value);return value;}
    private static string Json(ThemeDocument document){ThemeRules.Validate(document);var json=ContractJson.Write(document);if(Encoding.UTF8.GetByteCount(json)>65536)throw new DomainFault("ThemeTooLarge");return json;}
    private static ThemeView View(ThemeDraft value)=>new(value.Id,value.Name,value.Archived,Convert.ToBase64String(value.Version),Document(value.Document));
    private static void Page(int value){if(value is <1 or >10000)throw new DomainFault("InvalidPage");}
    private async Task<ThemeDraft> Find(Guid id,CancellationToken ct)=>await db.Themes.SingleOrDefaultAsync(t=>t.Id==id,ct)??throw new BusinessFault("ThemeNotFound",404);
    public async Task<IReadOnlyCollection<ThemeView>> List(Guid tenant,int page,CancellationToken ct)
    {Page(page);await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return(await db.Themes.AsNoTracking().OrderBy(t=>t.Name).ThenBy(t=>t.Id).Skip((page-1)*50).Take(50).ToListAsync(ct)).Select(View).ToArray();}
    public async Task<ThemeView> Create(Guid tenant,CreateTheme request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);var preset=await db.Presets.AsNoTracking().SingleOrDefaultAsync(p=>p.BusinessType==request.Preset,ct);
        var document=preset is null?ThemeRules.Default(request.Preset):Document(preset.Document);
        var theme=new ThemeDraft{TenantId=tenant,Name=DomainValues.Text(request.Name),Document=Json(document)};db.Themes.Add(theme);db.Record(scope,contexts,"theme.created",theme.Id.ToString());await db.SaveChangesAsync(ct);return View(theme);
    }
    public async Task<ThemeView> Save(Guid tenant,Guid id,SaveTheme request,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);var theme=await Find(id,ct);if(theme.Archived)throw new BusinessFault("ThemeArchived");db.ExpectedVersion(theme,request.Version);theme.Document=Json(request.Document);theme.UpdatedAt=DateTimeOffset.UtcNow;db.Record(scope,contexts,"theme.draft.updated",id.ToString());await db.SaveChangesAsync(ct);return View(theme);}
    private async Task<TenantTheme> Assignment(Guid tenant,string? expected,CancellationToken ct)
    {
        var publication=await db.Published.SingleOrDefaultAsync(p=>p.TenantId==tenant,ct);
        if(publication is null){if(!string.IsNullOrEmpty(expected))throw new BusinessFault("PublicationVersionConflict");publication=new TenantTheme{TenantId=tenant};db.Published.Add(publication);}
        else{if(string.IsNullOrEmpty(expected))throw new BusinessFault("PublicationVersionRequired");db.ExpectedVersion(publication,expected);}
        return publication;
    }
    public async Task<PublicationView> Publish(Guid tenant,Guid id,PublishTheme request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);PublishedSnapshot snapshot;PublicationView result;
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct))
        {
            var theme=await Find(id,ct);if(theme.Archived)throw new BusinessFault("ThemeArchived");db.ExpectedVersion(theme,request.Version);var json=Json(Document(theme.Document));
            var next=(await db.Versions.Where(v=>v.ThemeId==id).Select(v=>(int?)v.Revision).MaxAsync(ct)??0)+1;
            var version=new ThemeVersion{TenantId=tenant,ThemeId=id,Revision=next,Document=json,Hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))};db.Versions.Add(version);
            var assignment=await Assignment(tenant,request.PublicationVersion,ct);assignment.ThemeVersionId=version.Id;assignment.Sequence++;
            theme.UpdatedAt=DateTimeOffset.UtcNow;db.Publications.Add(new ThemePublication{TenantId=tenant,ThemeVersionId=version.Id,Sequence=assignment.Sequence});db.Record(scope,contexts,"theme.published",version.Id.ToString());
            db.Emit(scope,module,contexts,"theme.published.v1",new{versionId=version.Id,sequence=assignment.Sequence,document=Document(json),hash=version.Hash});
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            snapshot=new(assignment.Sequence,json,version.Hash,DateTimeOffset.UtcNow);result=new(version.Id,assignment.Sequence,Convert.ToBase64String(assignment.Version),Convert.ToBase64String(theme.Version));
        }
        await snapshots.PutAsync(tenant,"theme",snapshot,ct);return result;
    }
    public async Task<PublicationView> Rollback(Guid tenant,RollbackTheme request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);PublishedSnapshot snapshot;PublicationView result;
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct))
        {
            var version=await db.Versions.SingleOrDefaultAsync(v=>v.Id==request.ThemeVersionId,ct)??throw new BusinessFault("ThemeVersionNotFound",404);Document(version.Document);
            var assignment=await Assignment(tenant,request.PublicationVersion,ct);assignment.ThemeVersionId=version.Id;assignment.Sequence++;
            db.Publications.Add(new ThemePublication{TenantId=tenant,ThemeVersionId=version.Id,Sequence=assignment.Sequence,Rollback=true});db.Record(scope,contexts,"theme.rolled-back",version.Id.ToString());
            db.Emit(scope,module,contexts,"theme.published.v1",new{versionId=version.Id,sequence=assignment.Sequence,document=Document(version.Document),hash=version.Hash});await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            snapshot=new(assignment.Sequence,version.Document,version.Hash,DateTimeOffset.UtcNow);result=new(version.Id,assignment.Sequence,Convert.ToBase64String(assignment.Version),"");
        }
        await snapshots.PutAsync(tenant,"theme",snapshot,ct);return result;
    }
    public async Task<IReadOnlyCollection<VersionView>> Versions(Guid tenant,Guid id,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return await db.Versions.AsNoTracking().Where(v=>v.ThemeId==id).OrderByDescending(v=>v.Revision).Take(100).Select(v=>new VersionView(v.Id,v.ThemeId,v.Revision,v.Hash,v.CreatedAt)).ToListAsync(ct);}
    public async Task<ThemeDocument> Version(Guid tenant,Guid id,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);var value=await db.Versions.AsNoTracking().SingleOrDefaultAsync(v=>v.Id==id,ct)??throw new BusinessFault("ThemeVersionNotFound",404);return Document(value.Document);}
    public async Task<object> PublicationHistory(Guid tenant,int page,CancellationToken ct)
    {Page(page);await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return await db.Publications.AsNoTracking().OrderByDescending(p=>p.Sequence).Skip((page-1)*50).Take(50).Select(p=>new{p.ThemeVersionId,p.Sequence,p.Rollback,p.PublishedAt}).ToListAsync(ct);}
    public async Task<object> Presets(CancellationToken ct)=>await db.Presets.AsNoTracking().OrderBy(p=>p.BusinessType).Select(p=>new{p.BusinessType,p.Document,Version=Convert.ToBase64String(p.Version)}).ToListAsync(ct);
    public async Task SavePreset(string businessType,SavePreset request,CancellationToken ct)
    {
        access.GlobalPlatform("theme.preset.updated");var key=DomainValues.Text(businessType,40).ToLowerInvariant();var value=await db.Presets.SingleOrDefaultAsync(p=>p.BusinessType==key,ct);
        if(value is null){if(!string.IsNullOrEmpty(request.Version))throw new BusinessFault("PresetVersionConflict");value=new ThemePreset{BusinessType=key};db.Presets.Add(value);}
        else{if(string.IsNullOrEmpty(request.Version))throw new BusinessFault("PresetVersionRequired");db.ExpectedVersion(value,request.Version);}
        value.Document=Json(request.Document);value.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
    }
    public async Task<PublicTheme> Public(Guid tenant,string? businessType,string mode,bool mobile,CancellationToken ct)
    {
        if(tenant==Guid.Empty||mode is not("light"or"dark"))throw new DomainFault("InvalidThemeRequest");
        PublicTheme Result(ThemeDocument value,long sequence,string source,string type)=>new(tenant,sequence,source,type,value,ThemeRules.Variables(value,mode,mobile));
        bool Valid(PublishedSnapshot value,out ThemeDocument? doc)
        {
            doc=null;try{if(value.Sequence<1||string.IsNullOrEmpty(value.Document)||Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Document)))!=value.Hash)return false;doc=Document(value.Document);return true;}
            catch(Exception ex)when(ex is JsonException or DomainFault or BusinessFault){return false;}
        }
        var cached=await snapshots.GetAsync(tenant,"theme",ct);if(cached is not null&&Valid(cached,out var cachedDocument))return Result(cachedDocument!,cached.Sequence,"last-valid-snapshot",businessType??"neutral");
        scope.BindPublic(tenant);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var value=await(from publication in db.Published.AsNoTracking() join version in db.Versions.AsNoTracking() on new{publication.TenantId,Id=publication.ThemeVersionId} equals new{version.TenantId,version.Id} where publication.TenantId==tenant select new PublishedSnapshot(publication.Sequence,version.Document,version.Hash,version.CreatedAt)).SingleOrDefaultAsync(deadline.Token);
            if(value is not null&&Valid(value,out var doc)){await snapshots.PutAsync(tenant,"theme",value,ct);return Result(doc!,value.Sequence,"published",businessType??"neutral");}
        }
        catch(Exception ex)when(ex is SqlException or JsonException||ex is OperationCanceledException&&!ct.IsCancellationRequested){logger.LogWarning("Theme published store unavailable ({ErrorType}); using bundled fallback",ex.GetType().Name);}
        var type=businessType;
        try{type=(await remote.PublicTenantAsync(tenant,ct))?.BusinessType??type;}catch(DependencyFault){ }
        type=type is "spices"or"clothing"or"grocery"or"general-store"?type:"neutral";
        return Result(ThemeRules.Default(type),0,type=="neutral"?"neutral-default":"business-default",type);
    }
}
