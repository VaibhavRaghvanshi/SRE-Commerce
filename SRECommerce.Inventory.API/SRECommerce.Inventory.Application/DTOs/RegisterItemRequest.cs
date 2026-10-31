namespace SRECommerce.Inventory.Application;
public record RegisterItemRequest(Guid ProductId,Guid VariantId,decimal ReorderLevel=0,string? Version=null);
