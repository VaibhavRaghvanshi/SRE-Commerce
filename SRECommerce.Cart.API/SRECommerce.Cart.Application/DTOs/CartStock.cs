namespace SRECommerce.Cart.Application;
public sealed record CartStock(Guid VariantId,decimal Available,bool LowStock,bool Tracked);
