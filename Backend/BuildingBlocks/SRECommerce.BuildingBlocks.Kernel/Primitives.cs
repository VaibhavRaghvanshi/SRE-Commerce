namespace SRECommerce.BuildingBlocks.Kernel;

public interface ITenantRecord {Guid TenantId {get;set;}}
public interface IVersionedRecord {byte[] Version {get;set;}}
public class DomainFault(string code,int status=400):Exception(code)
{public string Code{get;}=code;public int Status{get;}=status;}
public static class DomainValues
{
    public static string Text(string value,int max=160)
    {if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>max||value.Any(char.IsControl))throw new DomainFault("InvalidText");return value.Trim();}
    public static decimal Quantity(decimal value,bool allowZero=false)
    {if(value<0||(!allowZero&&value==0)||value>1000000||decimal.Round(value,3)!=value)throw new DomainFault("InvalidQuantity");return value;}
    public static decimal Amount(decimal value)
    {if(value<0||value>1000000000000m||decimal.Round(value,2)!=value)throw new DomainFault("InvalidAmount");return value;}
    public static string Currency(string value)
    {if(value.Length!=3||value.Any(c=>c is <'A' or >'Z'))throw new DomainFault("InvalidCurrency");return value;}
}
