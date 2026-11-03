using System.Security.Cryptography;using System.Text;using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public static class ShippingRules
{
 public static void Id(Guid id){if(id==Guid.Empty)throw new DomainFault("InvalidShippingId");}
 public static string Text(string value,int max=160){if(string.IsNullOrWhiteSpace(value)||value.Length>max||value.Any(char.IsControl)||value.Contains('<')||value.Contains('>'))throw new DomainFault("InvalidShippingText");return value.Trim();}
 public static string Code(string value){var code=Text(value,64).ToUpperInvariant();if(code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))throw new DomainFault("InvalidShippingCode");return code;}
 public static string Country(string value){var code=value.Trim().ToUpperInvariant();if(code.Length!=2||code.Any(c=>c is <'A' or >'Z'))throw new DomainFault("InvalidShippingCountry");return code;}
 public static string Postal(string value){var code=value.Trim().ToUpperInvariant();if(code.Length is <2 or >16||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!=' '&&c!='-'))throw new DomainFault("InvalidShippingPostalCode");return code;}
 public static void Money(decimal value){if(value<0||value>1000000000000m||decimal.Round(value,4)!=value)throw new DomainFault("InvalidShippingAmount");}
 public static void Quantity(decimal value){if(value<=0||value>1000||decimal.Round(value,3)!=value)throw new DomainFault("InvalidShippingQuantity");}
 public static bool Region(string[] countries,string[] prefixes,string country,string postal)=>countries.Contains(country)&&(!prefixes.Any()||prefixes.Any(p=>postal.Replace(" ","").StartsWith(p.Replace(" ",""),StringComparison.Ordinal)));
 public static decimal Round(decimal amount,int digits)=>decimal.Round(amount,digits,MidpointRounding.AwayFromZero);
 public static Guid Operation(Guid id,string action)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("N")+":"+action)).AsSpan(0,16));
 public static void Move(Shipment shipment,ShipmentStatus target)
 {
  var valid=shipment.Status switch{ShipmentStatus.Created=>target is ShipmentStatus.Packed or ShipmentStatus.Cancelled,ShipmentStatus.Packed=>target is ShipmentStatus.Dispatched or ShipmentStatus.Cancelled,ShipmentStatus.Dispatched=>target is ShipmentStatus.Delivered or ShipmentStatus.DeliveryException,ShipmentStatus.DeliveryException=>target is ShipmentStatus.Dispatched or ShipmentStatus.Delivered,_=>false};
  if(!valid)throw new DomainFault("InvalidShipmentTransition",409);shipment.Status=target;
 }
}
