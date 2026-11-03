namespace SRECommerce.Shipping.Application;
public interface ICourierProvider { string Name{get;}string Reference(Guid shipment); }
