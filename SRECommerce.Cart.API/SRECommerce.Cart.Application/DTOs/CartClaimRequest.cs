namespace SRECommerce.Cart.Application;
public sealed record CartClaimRequest(Guid OrderId,Guid PreparationId,Guid? UserId,string? TenantRole=null);
