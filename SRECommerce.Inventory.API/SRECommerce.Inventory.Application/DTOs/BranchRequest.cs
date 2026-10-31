namespace SRECommerce.Inventory.Application;
public record BranchRequest(string Code,string Name,Guid? TenantLocationId=null,bool Active=true,string? Version=null);
