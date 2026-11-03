using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class SandboxCourierProvider:ICourierProvider
{public string Name=>"SandboxCourier";public string Reference(Guid shipment)=>"sandbox-courier:"+shipment.ToString("N");}
