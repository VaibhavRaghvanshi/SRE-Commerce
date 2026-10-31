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
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Infrastructure;
public sealed class StorefrontOperations(StorefrontDbContext db,ModuleAccess access,ModuleScope scope,ServiceSpec module,IHttpContextAccessor contexts,IPublishedSnapshots snapshots,RemoteAccess remote,ILogger<StorefrontOperations> logger):IStorefrontOperations
{
    private static LayoutDocument Document(string json){var value=ContractJson.Read<LayoutDocument>(json);LayoutRules.Validate(value);return value;}
    private static string Json(LayoutDocument document){LayoutRules.Validate(document);var value=ContractJson.Write(document);if(Encoding.UTF8.GetByteCount(value)>131072)throw new DomainFault("LayoutTooLarge");return value;}
    private static string Kind(string slug)=>"layout-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slug)))[..24];
    private static PageView View(StorefrontPage value)=>new(value.Id,value.Slug,value.Title,Convert.ToBase64String(value.Version),Document(value.Document));
    private static void Page(int page){if(page is <1 or >10000)throw new DomainFault("InvalidPage");}
    private async Task<StorefrontPage> Find(Guid id,CancellationToken ct)=>await db.Pages.SingleOrDefaultAsync(p=>p.Id==id,ct)??throw new BusinessFault("PageNotFound",404);
    public async Task<IReadOnlyCollection<PageView>> List(Guid tenant,int page,CancellationToken ct)
    {Page(page);await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return(await db.Pages.AsNoTracking().OrderBy(p=>p.Slug).Skip((page-1)*50).Take(50).ToListAsync(ct)).Select(View).ToArray();}
    public async Task<PageView> Create(Guid tenant,CreatePage request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);var slug=LayoutRules.Slug(request.Slug);var title=DomainValues.Text(request.Title);var template=await db.Templates.AsNoTracking().SingleOrDefaultAsync(t=>t.BusinessType==request.Template,ct);
        var page=new StorefrontPage{TenantId=tenant,Slug=slug,Title=title,Document=Json(template is null?LayoutRules.Default(request.Template):Document(template.Document))};
        db.Pages.Add(page);db.Record(scope,contexts,"storefront.page.created",page.Id.ToString());await db.SaveChangesAsync(ct);return View(page);
    }
    public async Task<PageView> Save(Guid tenant,Guid id,SavePage request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);var page=await Find(id,ct);db.ExpectedVersion(page,request.Version);page.Title=DomainValues.Text(request.Title);page.Document=Json(request.Document);page.UpdatedAt=DateTimeOffset.UtcNow;
        db.Record(scope,contexts,"storefront.draft.updated",id.ToString());await db.SaveChangesAsync(ct);return View(page);
    }
    private async Task<PublishedConfiguration> Assignment(Guid tenant,Guid page,string? expected,CancellationToken ct)
    {
        var value=await db.Published.SingleOrDefaultAsync(p=>p.PageId==page,ct);
        if(value is null){if(!string.IsNullOrEmpty(expected))throw new BusinessFault("PublicationVersionConflict");value=new(){TenantId=tenant,PageId=page};db.Published.Add(value);}
        else{if(string.IsNullOrEmpty(expected))throw new BusinessFault("PublicationVersionRequired");db.ExpectedVersion(value,expected);}
        return value;
    }
    public async Task<PublicationView> Publish(Guid tenant,Guid id,PublishPage request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);PublishedSnapshot snapshot;PublicationView result;string slug;
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct))
        {
            var page=await Find(id,ct);db.ExpectedVersion(page,request.Version);slug=page.Slug;var json=Json(Document(page.Document));
            var revision=(await db.Versions.Where(v=>v.PageId==id).Select(v=>(int?)v.Revision).MaxAsync(ct)??0)+1;
            var version=new LayoutVersion{TenantId=tenant,PageId=id,Revision=revision,Document=json,Hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))};db.Versions.Add(version);
            var assignment=await Assignment(tenant,id,request.PublicationVersion,ct);assignment.LayoutVersionId=version.Id;assignment.Sequence++;page.UpdatedAt=DateTimeOffset.UtcNow;
            db.Publications.Add(new(){TenantId=tenant,PageId=id,LayoutVersionId=version.Id,Sequence=assignment.Sequence});db.Record(scope,contexts,"storefront.published",version.Id.ToString());
            db.Emit(scope,module,contexts,"storefront.published.v1",new{pageId=id,slug,versionId=version.Id,sequence=assignment.Sequence,document=Document(json),hash=version.Hash});
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            snapshot=new(assignment.Sequence,json,version.Hash,DateTimeOffset.UtcNow);result=new(version.Id,assignment.Sequence,Convert.ToBase64String(assignment.Version),Convert.ToBase64String(page.Version));
        }
        await snapshots.PutAsync(tenant,Kind(slug),snapshot,ct);return result;
    }
    public async Task<PublicationView> Rollback(Guid tenant,Guid id,RollbackPage request,CancellationToken ct)
    {
        await access.AuthorizeAsync(tenant,ActorPermission.Manager,ct);PublishedSnapshot snapshot;PublicationView result;string slug;
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct))
        {
            var page=await Find(id,ct);slug=page.Slug;var version=await db.Versions.SingleOrDefaultAsync(v=>v.Id==request.LayoutVersionId&&v.PageId==id,ct)??throw new BusinessFault("LayoutVersionNotFound",404);Document(version.Document);
            var assignment=await Assignment(tenant,id,request.PublicationVersion,ct);assignment.LayoutVersionId=version.Id;assignment.Sequence++;
            db.Publications.Add(new(){TenantId=tenant,PageId=id,LayoutVersionId=version.Id,Sequence=assignment.Sequence,Rollback=true});db.Record(scope,contexts,"storefront.rolled-back",version.Id.ToString());
            db.Emit(scope,module,contexts,"storefront.published.v1",new{pageId=id,slug,versionId=version.Id,sequence=assignment.Sequence,document=Document(version.Document),hash=version.Hash});await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            snapshot=new(assignment.Sequence,version.Document,version.Hash,DateTimeOffset.UtcNow);result=new(version.Id,assignment.Sequence,Convert.ToBase64String(assignment.Version),Convert.ToBase64String(page.Version));
        }
        await snapshots.PutAsync(tenant,Kind(slug),snapshot,ct);return result;
    }
    public async Task<IReadOnlyCollection<VersionView>> Versions(Guid tenant,Guid id,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return await db.Versions.AsNoTracking().Where(v=>v.PageId==id).OrderByDescending(v=>v.Revision).Take(100).Select(v=>new VersionView(v.Id,v.PageId,v.Revision,v.Hash,v.CreatedAt)).ToListAsync(ct);}
    public async Task<LayoutDocument> Version(Guid tenant,Guid id,Guid version,CancellationToken ct)
    {await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return Document((await db.Versions.AsNoTracking().SingleOrDefaultAsync(v=>v.Id==version&&v.PageId==id,ct)??throw new BusinessFault("LayoutVersionNotFound",404)).Document);}
    public async Task<object> History(Guid tenant,Guid id,int page,CancellationToken ct)
    {Page(page);await access.AuthorizeAsync(tenant,ActorPermission.Staff,ct);return await db.Publications.AsNoTracking().Where(p=>p.PageId==id).OrderByDescending(p=>p.Sequence).Skip((page-1)*50).Take(50).Select(p=>new{p.LayoutVersionId,p.Sequence,p.Rollback,p.PublishedAt}).ToListAsync(ct);}
    public async Task<object> Templates(CancellationToken ct)=>await db.Templates.AsNoTracking().OrderBy(t=>t.BusinessType).Select(t=>new{t.BusinessType,t.Document,Version=Convert.ToBase64String(t.Version)}).ToListAsync(ct);
    public async Task SaveTemplate(string businessType,SaveTemplate request,CancellationToken ct)
    {
        access.GlobalPlatform("storefront.template.updated");var key=DomainValues.Text(businessType,40).ToLowerInvariant();var value=await db.Templates.SingleOrDefaultAsync(t=>t.BusinessType==key,ct);
        if(value is null){if(!string.IsNullOrEmpty(request.Version))throw new BusinessFault("TemplateVersionConflict");value=new(){BusinessType=key};db.Templates.Add(value);}
        else{if(string.IsNullOrEmpty(request.Version))throw new BusinessFault("TemplateVersionRequired");db.ExpectedVersion(value,request.Version);}
        value.Document=Json(request.Document);value.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
    }
    public async Task<PublicLayout> Public(Guid tenant,string slug,string? businessType,CancellationToken ct)
    {
        if(tenant==Guid.Empty)throw new DomainFault("InvalidTenant");slug=LayoutRules.Slug(slug);
        PublicLayout Result(LayoutDocument doc,long sequence,string source)=>new(tenant,slug,sequence,source,doc.Template,doc);
        bool Valid(PublishedSnapshot snapshot,out LayoutDocument? document)
        {document=null;try{if(snapshot.Sequence<1||Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Document)))!=snapshot.Hash)return false;document=Document(snapshot.Document);return true;}catch(Exception ex)when(ex is JsonException or DomainFault or BusinessFault){return false;}}
        var cached=await snapshots.GetAsync(tenant,Kind(slug),ct);if(cached is not null&&Valid(cached,out var document))return Result(document!,cached.Sequence,"last-valid-snapshot");
        scope.BindPublic(tenant);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var value=await(from publication in db.Published.AsNoTracking() join version in db.Versions.AsNoTracking() on new{publication.TenantId,Id=publication.LayoutVersionId} equals new{version.TenantId,version.Id} join page in db.Pages.AsNoTracking() on new{publication.TenantId,Id=publication.PageId} equals new{page.TenantId,page.Id} where page.Slug==slug select new PublishedSnapshot(publication.Sequence,version.Document,version.Hash,version.CreatedAt)).SingleOrDefaultAsync(deadline.Token);
            if(value is not null&&Valid(value,out document)){await snapshots.PutAsync(tenant,Kind(slug),value,ct);return Result(document!,value.Sequence,"published");}
        }
        catch(Exception ex)when(ex is SqlException or JsonException||ex is OperationCanceledException&&!ct.IsCancellationRequested){logger.LogWarning("Layout published store unavailable ({ErrorType}); using bundled fallback",ex.GetType().Name);}
        var type=businessType;try{type=(await remote.PublicTenantAsync(tenant,ct))?.BusinessType??type;}catch(DependencyFault){ }
        var fallback=LayoutRules.Default(type??"neutral");return Result(fallback,0,fallback.Template=="neutral"?"neutral-default":"business-default");
    }
}
public sealed class StorefrontInitializer(StorefrontDbContext db):IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        foreach(var type in new[]{"neutral","spices","clothing","grocery","general-store","custom"})if(!await db.Templates.AnyAsync(t=>t.BusinessType==type,ct)){var value=LayoutRules.Default(type);LayoutRules.Validate(value);db.Templates.Add(new(){BusinessType=type,Document=ContractJson.Write(value)});}
        await db.SaveChangesAsync(ct);
    }
}
