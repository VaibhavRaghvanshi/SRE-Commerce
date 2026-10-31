namespace SRECommerce.Inventory.Application;
public record SupplierRequest(string Code,string Name,string? ContactEmail=null,bool Active=true,string? Version=null);
