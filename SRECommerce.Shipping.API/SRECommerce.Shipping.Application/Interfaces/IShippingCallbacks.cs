namespace SRECommerce.Shipping.Application;
public interface IShippingCallbacks { Task Run(CancellationToken ct); }
