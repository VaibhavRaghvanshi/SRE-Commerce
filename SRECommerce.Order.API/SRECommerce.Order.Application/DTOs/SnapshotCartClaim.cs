using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotCartClaim(Guid OrderId,int Status,DateTimeOffset ExpiresAt,string Version,SnapshotPreparation Preparation);
