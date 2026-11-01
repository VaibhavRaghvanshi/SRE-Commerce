using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotPreparation(Guid Id,Guid CartId,Guid TenantId,Guid? UserId,int CartRevision,DateTimeOffset ExpiresAt,SnapshotLine[] Items,SnapshotQuote Quote,string? CustomerRole=null);
