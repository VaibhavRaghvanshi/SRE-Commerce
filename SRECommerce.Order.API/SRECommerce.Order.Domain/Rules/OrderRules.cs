using System.Security.Cryptography;using System.Text;using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public static class OrderRules
{
 public static byte[] Capability(string? value)
 {
  if(value is null||value.Length!=43||value.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))throw new DomainFault("OrderCapabilityRequired",401);
  byte[] bytes;try{bytes=Convert.FromBase64String(value.Replace('-','+').Replace('_','/')+"=");}catch(FormatException){throw new DomainFault("InvalidOrderCapability",401);}
  if(bytes.Length!=32||Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')!=value)throw new DomainFault("InvalidOrderCapability",401);return SHA256.HashData(bytes);
 }
 public static void Quantity(decimal value){if(value<=0||value>1000||decimal.Round(value,3)!=value)throw new DomainFault("InvalidOrderQuantity");}
 public static void Money(decimal value){if(value<0||value>1000000000000m||decimal.Round(value,4)!=value)throw new DomainFault("InvalidOrderAmount");}
 public static string Text(string value,int length=160){if(string.IsNullOrWhiteSpace(value)||value.Length>length||value.Any(char.IsControl)||value.Contains('<')||value.Contains('>'))throw new DomainFault("InvalidOrderText");return value.Trim();}
 public static string Payment(string value)=>value is "COD" or "UPI" or "Card" or "NetBanking"?value:throw new DomainFault("UnsupportedPaymentMethod");
 public static Guid Operation(Guid order,string action)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(order.ToString("N")+":"+action)).AsSpan(0,16));
 public static void Move(SalesOrder order,OrderStatus target)
 {
  var allowed=order.Status switch
  {
   OrderStatus.AwaitingPayment=>target is OrderStatus.Confirmed or OrderStatus.CancellationPending or OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.ManualReview,
   OrderStatus.Confirmed=>target is OrderStatus.Processing or OrderStatus.PartiallyFulfilled or OrderStatus.Fulfilled or OrderStatus.CancellationPending or OrderStatus.Cancelled or OrderStatus.ManualReview,
   OrderStatus.Processing=>target is OrderStatus.PartiallyFulfilled or OrderStatus.Fulfilled or OrderStatus.CancellationPending or OrderStatus.Cancelled or OrderStatus.ManualReview,
   OrderStatus.PartiallyFulfilled=>target is OrderStatus.Fulfilled or OrderStatus.ManualReview,
   OrderStatus.Fulfilled=>target is OrderStatus.Delivered or OrderStatus.ManualReview,
   OrderStatus.CancellationPending=>target is OrderStatus.Cancelled or OrderStatus.ManualReview,
   OrderStatus.Delivered=>target is OrderStatus.Completed or OrderStatus.ManualReview,
   _=>false
  };
  if(!allowed)throw new DomainFault("InvalidOrderTransition",409);order.Status=target;
 }
 public static void Lines(SalesOrder order)
 {if(order.Items.Count is <1 or >50||order.Items.Select(i=>i.VariantId).Distinct().Count()!=order.Items.Count)throw new DomainFault("InvalidOrderItems");foreach(var i in order.Items){Quantity(i.Quantity);if(i.ShipmentRequested<0||i.ShipmentRequested>i.Quantity||i.Fulfilled>i.ShipmentRequested||i.RefundAllocated<0||i.RefundAllocated>i.Total||i.Fulfilled<0||i.Delivered<0||i.ReturnRequested<0||i.Returned<0||i.Fulfilled>i.Quantity||i.Delivered>i.Fulfilled||i.ReturnRequested>i.Delivered||i.Returned>i.ReturnRequested)throw new DomainFault("OrderLineQuantityExceeded",409);}}
}
