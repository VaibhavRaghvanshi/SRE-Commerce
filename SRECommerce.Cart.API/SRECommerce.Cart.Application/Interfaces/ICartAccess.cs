using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Cart.Application;
public interface ICartAccess
{
 UserAccess? Actor {get;}Task Authorize(Guid tenant,bool guest,CancellationToken ct);Task Machine(Guid tenant,CancellationToken ct);
 Task<CartCatalogProduct[]> Products(Guid tenant,Guid[] variants,bool guest,CancellationToken ct);
 Task<CartStock[]> Availability(Guid tenant,Guid[] variants,bool guest,CancellationToken ct);
 Task<CartPriceQuote> Quote(Guid tenant,Guid id,CartQuoteItem[] items,string? coupon,bool guest,CancellationToken ct);
}
