using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Application;
public sealed record CartView(Guid Id,Guid TenantId,bool Guest,CartStatus Status,int Revision,DateTimeOffset ExpiresAt,CartLineView[] Items,string Version);
