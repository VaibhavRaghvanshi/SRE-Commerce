namespace SRECommerce.Cart.Application;
public sealed record CartPreview(CartView Cart,CartPriceQuote Quote,CartStock[] Stock,bool CanPrepare);
