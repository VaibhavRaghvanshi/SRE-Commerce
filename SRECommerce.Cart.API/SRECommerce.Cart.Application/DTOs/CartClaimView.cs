using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Application;
public sealed record CartClaimView(Guid OrderId,CartClaimStatus Status,DateTimeOffset ExpiresAt,string Version,PreparedCartView Preparation);
