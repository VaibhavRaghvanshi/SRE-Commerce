using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public static partial class InventoryRules
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{0,63}$")] private static partial Regex CodePattern();
    public static string Code(string value)
    {var code=Text(value,64).ToUpperInvariant();if(!CodePattern().IsMatch(code))throw new DomainFault("InvalidInventoryCode");return code;}
    public static string Text(string value,int max=160,bool optional=false)
    {if(optional&&string.IsNullOrWhiteSpace(value))return "";if(string.IsNullOrWhiteSpace(value)||value.Length>max||value.Any(char.IsControl)||value.Contains('<')||value.Contains('>'))throw new DomainFault("InvalidInventoryText");return value.Trim();}
    public static decimal Quantity(decimal value,bool positive=true)
    {if(value>1000000000m||value< -1000000000m||decimal.Round(value,3)!=value||positive&&value<=0)throw new DomainFault("InvalidStockQuantity");return value;}
    public static void Balance(decimal onHand,decimal reserved,decimal damaged)
    {if(onHand<0||reserved<0||damaged<0||reserved>onHand)throw new DomainFault("InsufficientAvailableStock",409);}
    public static void Reservation(DateTimeOffset now,DateTimeOffset expires)
    {if(expires<now.AddSeconds(30)||expires>now.AddHours(1))throw new DomainFault("InvalidReservationExpiry");}
}
