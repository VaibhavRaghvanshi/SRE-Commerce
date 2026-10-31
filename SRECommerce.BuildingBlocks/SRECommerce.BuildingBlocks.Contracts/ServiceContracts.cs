using System.Text.Json;

namespace SRECommerce.BuildingBlocks.Contracts;

public sealed record ServiceSpec(string Name,string Module,int Port)
{
    public string Database => $"SRECommerce_{Name}DB";
    public string TestPrefix => $"SRECommerce_{Name}Tests_";
    public string InternalScope => "internal_"+Name.ToLowerInvariant();
}
public static class ServiceManifest
{
    public static readonly ServiceSpec[] All =
    [new("Identity","identity",5081),new("Tenant","tenant",5082),new("Theme","themes",5083),new("Storefront","storefront",5084),new("Catalog","catalog",5085),new("Inventory","inventory",5086),new("Pricing","pricing",5087),new("Cart","cart",5088),new("Order","orders",5089),new("Payment","payments",5090),new("Shipping","shipping",5091),new("Notification","notifications",5092),new("Manufacturing","manufacturing",5093),new("Discovery","discovery",5094)];
    public static ServiceSpec Get(string name)=>All.Single(s=>s.Name.Equals(name,StringComparison.Ordinal));
    public static string ClientId(string name)=>"srecommerce-service-"+Get(name).Name.ToLowerInvariant();
    public static string[] Targets(string source)=>source switch
    {
        "Identity"=>["Tenant"],
        "Theme"=>["Tenant"],
        "Storefront"=>["Tenant","Catalog","Theme"],
        "Catalog"=>["Tenant"],
        "Inventory"=>["Tenant","Catalog"],
        "Pricing"=>["Tenant","Catalog"],
        "Cart"=>["Tenant","Catalog","Pricing","Inventory"],
        "Order"=>["Tenant","Catalog","Pricing","Inventory","Cart","Payment","Shipping","Notification"],
        "Payment" or "Shipping"=>["Tenant","Order"],
        "Notification"=>["Tenant","Order"],
        "Manufacturing"=>["Tenant","Catalog","Inventory","Pricing"],
        "Discovery"=>["Tenant","Catalog","Order"],
        _=>[]
    };
    public static string[] Scopes(string source)=>Targets(source).Select(name=>Get(name).InternalScope).Distinct().ToArray();
}
public sealed record UserAccess(Guid UserId,string AccountKind,bool IsPlatformAdmin,Guid? SelectedTenantId,string? TenantRole,string[] Scopes,string[] Audiences,string? Email=null);
public sealed record MachineAccess(string Service,string ClientId,string[] Scopes,string[] Audiences);
public sealed record ModuleFlag(string Module,bool Enabled);
public sealed record TenantAccess(Guid TenantId,bool Active,ModuleFlag[] Modules);
public sealed record PublicTenant(Guid TenantId,string Slug,string BusinessType,string DisplayName,string? LogoUrl,string Currency,string[] Modules);
public sealed record ProvisioningLimits(Guid TenantId,bool Allowed,int MaxUsers);
public sealed record EventEnvelope(Guid Id,int SchemaVersion,string Producer,string Type,Guid TenantId,string CorrelationId,DateTimeOffset OccurredAt,JsonElement Data);
public enum ActorPermission { Customer, Staff, Manager, Owner }
public sealed class BusinessFault(string code,int status=409) : Exception(code)
{
    public string Code { get; }=code;
    public int Status { get; }=status;
}
public sealed class DependencyFault(string dependency) : Exception("Required dependency unavailable") {public string Dependency {get;}=dependency;}
public interface ITenantRecord : Kernel.ITenantRecord;
public interface IVersionedRecord : Kernel.IVersionedRecord;
public interface IIntegrationHandler
{
    IReadOnlyCollection<string> EventTypes {get;}
    Task HandleAsync(EventEnvelope message,CancellationToken ct);
}
public static class ContractJson
{
    public static readonly JsonSerializerOptions Strict=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    public static T Read<T>(string json)=>JsonSerializer.Deserialize<T>(json,Strict)??throw new BusinessFault("InvalidDocument",400);
    public static string Write<T>(T value)=>JsonSerializer.Serialize(value,Strict);
}
