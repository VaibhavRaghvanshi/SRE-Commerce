using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ShippingQuote : ITenantRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;} public Guid MethodId {get;set;}
 public Guid PreparationId {get;set;} public Guid? UserId {get;set;}
 public string Document {get;set;}="{}"; public string ConfigurationSnapshot {get;set;}="{}";
 public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset ExpiresAt {get;set;}
}
