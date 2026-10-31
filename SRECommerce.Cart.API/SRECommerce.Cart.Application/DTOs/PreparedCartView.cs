namespace SRECommerce.Cart.Application;
public sealed record PreparedCartView(Guid Id,Guid CartId,Guid TenantId,Guid? UserId,int CartRevision,DateTimeOffset ExpiresAt,CartLineView[] Items,CartPriceQuote Quote);
