using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public sealed class ModuleScope
{
    public Guid? TenantId {get;private set;}
    public UserAccess? User {get;private set;}
    public MachineAccess? Machine {get;private set;}
    public string? SupportReason {get;private set;}
    public bool Bootstrap {get;private set;}
    public bool GlobalCatalog {get;private set;}
    public void BindUser(Guid tenant,UserAccess actor,string? reason=null)
    {if(tenant==Guid.Empty)throw new BusinessFault("InvalidTenant",400);TenantId=tenant;User=actor;Machine=null;SupportReason=reason;}
    public void BindMachine(Guid tenant,MachineAccess actor)
    {if(tenant==Guid.Empty)throw new BusinessFault("InvalidTenant",400);TenantId=tenant;Machine=actor;User=null;}
    public void BindPublic(Guid tenant)
    {if(tenant==Guid.Empty)throw new BusinessFault("InvalidTenant",400);TenantId=tenant;User=null;Machine=null;}
    public void BindEvent(Guid tenant,string producer)
    {BindMachine(tenant,new(producer,"broker:"+producer,[],[]));}
    public void BindBootstrap(){Bootstrap=true;GlobalCatalog=true;TenantId=Guid.Empty;}
    public void BindGlobal(UserAccess actor,string reason)
    {if(!actor.IsPlatformAdmin)throw new BusinessFault("PlatformAdministratorRequired",403);User=actor;GlobalCatalog=true;SupportReason=reason;TenantId=Guid.Empty;}
    public bool CanWrite=>Bootstrap || User is not null || Machine is not null;
    public Guid? ActorId=>User?.UserId;
    public string ActorName=>Machine?.Service??(Bootstrap?"bootstrap":"user");
}
public sealed class OutboxMessage : ITenantRecord
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid TenantId {get;set;}
    public string Type {get;set;}="";public string Envelope {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;public DateTimeOffset? PublishedAt {get;set;}
    public int Attempts {get;set;} public DateTimeOffset NextAttemptAt {get;set;}=DateTimeOffset.UtcNow;
    public Guid? LeaseId {get;set;}public DateTimeOffset? LeaseUntil {get;set;}public byte[] Version {get;set;}=[];
}
public sealed class InboxMessage : ITenantRecord
{
    public Guid Id {get;set;} public Guid TenantId {get;set;}public string Producer {get;set;}="";
    public string Type {get;set;}="";public string PayloadHash {get;set;}="";public DateTimeOffset ProcessedAt {get;set;}=DateTimeOffset.UtcNow;
}
public sealed class AuditEntry : ITenantRecord
{
    public long Id {get;set;}public Guid TenantId {get;set;}public Guid? ActorId {get;set;}
    public string ActorService {get;set;}="";public string Action {get;set;}="";public string Resource {get;set;}="";
    public string? SupportReason {get;set;}public string CorrelationId {get;set;}="";public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow;
}
public interface IModuleStore
{
    DbContext Context {get;}DbSet<OutboxMessage> Outbox {get;}DbSet<InboxMessage> Inbox {get;}DbSet<AuditEntry> Audit {get;}
}
public static class ModuleEf
{
    public static void ConfigureInfrastructure(this ModelBuilder model)
    {
        model.Entity<OutboxMessage>(e=>{e.ToTable("IntegrationOutbox");e.HasKey(x=>x.Id);e.Property(x=>x.Type).HasMaxLength(120);e.Property(x=>x.Envelope).HasMaxLength(262144);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.PublishedAt,x.NextAttemptAt});e.HasIndex(x=>x.LeaseUntil);});
        model.Entity<InboxMessage>(e=>{e.ToTable("IntegrationInbox");e.HasKey(x=>x.Id);e.Property(x=>x.Producer).HasMaxLength(40);e.Property(x=>x.Type).HasMaxLength(120);e.Property(x=>x.PayloadHash).HasMaxLength(64);e.HasIndex(x=>new{x.TenantId,x.ProcessedAt});});
        model.Entity<AuditEntry>(e=>{e.ToTable("AuditEntries");e.HasKey(x=>x.Id);e.Property(x=>x.ActorService).HasMaxLength(40);e.Property(x=>x.Action).HasMaxLength(120);e.Property(x=>x.Resource).HasMaxLength(160);e.Property(x=>x.SupportReason).HasMaxLength(300);e.Property(x=>x.CorrelationId).HasMaxLength(100);e.HasIndex(x=>new{x.TenantId,x.Id});});
    }
    public static void TenantEntity<T>(this EntityTypeBuilder<T> entity,System.Linq.Expressions.Expression<Func<T,bool>> filter)where T:class,Kernel.ITenantRecord
    {entity.HasQueryFilter(filter);entity.Property(x=>x.TenantId).IsRequired();}
    public static void GuardWrites(this DbContext db,ModuleScope scope)
    {
        foreach(var entry in db.ChangeTracker.Entries().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if(entry.State==EntityState.Deleted)throw new BusinessFault("PhysicalDeletionNotAllowed",403);
            if(entry.Entity is not Kernel.ITenantRecord tenantRecord)
            {if(!scope.CanWrite||!scope.GlobalCatalog||entry.Metadata.FindAnnotation("SRECommerce:GlobalCatalog")?.Value is not true)throw new BusinessFault("GlobalCatalogScopeRequired",403);continue;}
            if(!scope.CanWrite || scope.TenantId!=tenantRecord.TenantId)throw new BusinessFault("TenantWriteScopeMismatch",403);
            if(entry.State==EntityState.Modified && entry.Property(nameof(Kernel.ITenantRecord.TenantId)).IsModified)throw new BusinessFault("TenantIdentityImmutable",403);
        }
    }
    public static void ExpectedVersion<T>(this DbContext db,T entity,string version)where T:class,Kernel.IVersionedRecord
    {
        byte[] value;try{value=Convert.FromBase64String(version);}catch(FormatException){throw new BusinessFault("InvalidVersion",400);}
        if(value.Length!=8)throw new BusinessFault("InvalidVersion",400);
        if(!CryptographicOperations.FixedTimeEquals(value,entity.Version))throw new BusinessFault("VersionConflict");
        db.Entry(entity).Property(nameof(IVersionedRecord.Version)).OriginalValue=value;
    }
    public static void Record(this IModuleStore store,ModuleScope scope,IHttpContextAccessor contexts,string action,string resource)
    {
        if(scope.TenantId is not {} tenant || !scope.CanWrite)throw new BusinessFault("AuditScopeRequired",403);
        store.Audit.Add(new AuditEntry{TenantId=tenant,ActorId=scope.ActorId,ActorService=scope.ActorName,Action=action,Resource=resource,SupportReason=scope.SupportReason,CorrelationId=Correlation(contexts)});
    }
    public static Guid Emit<T>(this IModuleStore store,ModuleScope scope,ServiceSpec service,IHttpContextAccessor contexts,string type,T data)
    {
        if(scope.TenantId is not {} tenant || tenant==Guid.Empty || !scope.CanWrite || !type.StartsWith(service.Name.ToLowerInvariant()+".",StringComparison.Ordinal))throw new BusinessFault("InvalidEventScope",500);
        var id=Guid.NewGuid();var envelope=new EventEnvelope(id,1,service.Name,type,tenant,Correlation(contexts),DateTimeOffset.UtcNow,JsonSerializer.SerializeToElement(data,ContractJson.Strict));
        var json=ContractJson.Write(envelope);if(System.Text.Encoding.UTF8.GetByteCount(json)>262144)throw new BusinessFault("EventTooLarge",400);
        store.Outbox.Add(new OutboxMessage{Id=id,TenantId=tenant,Type=type,Envelope=json});return id;
    }
    public static string Correlation(IHttpContextAccessor accessor)=>accessor.HttpContext?.TraceIdentifier??System.Diagnostics.Activity.Current?.TraceId.ToString()??Guid.NewGuid().ToString("N");
}
