using System.Security.Cryptography;
using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public static class CartRules
{
 public static void Quantity(decimal value){if(value<=0||value>1000||decimal.Round(value,3)!=value)throw new DomainFault("InvalidCartQuantity");}
 public static byte[] Capability(string? value)
 {
  if(value is null||value.Length!=43||value.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))throw new DomainFault("CartCapabilityRequired",401);
  byte[] bytes;try{bytes=Convert.FromBase64String(value.Replace('-','+').Replace('_','/')+"=");}catch(FormatException){throw new DomainFault("InvalidCartCapability",401);}
  if(bytes.Length!=32||Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')!=value)throw new DomainFault("InvalidCartCapability",401);
  return SHA256.HashData(bytes);
 }
 public static void Match(byte[] expected,string? capability){if(!CryptographicOperations.FixedTimeEquals(expected,Capability(capability)))throw new DomainFault("CartNotFound",404);}
}
