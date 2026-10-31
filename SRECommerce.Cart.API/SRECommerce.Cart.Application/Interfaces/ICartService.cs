namespace SRECommerce.Cart.Application;
public interface ICartService
{
 Task<CartView> Create(Guid tenant,CreateCartRequest request,bool guest,string? capability,CancellationToken ct);
 Task<CartView> Get(Guid tenant,Guid id,bool guest,string? capability,CancellationToken ct);
 Task<CartView> Item(Guid tenant,Guid id,CartItemRequest request,bool guest,string? capability,CancellationToken ct);
 Task<CartView> Remove(Guid tenant,Guid id,Guid variant,RemoveCartItemRequest request,bool guest,string? capability,CancellationToken ct);
 Task<CartView> Merge(Guid tenant,Guid id,MergeCartRequest request,string? guestCapability,CancellationToken ct);
 Task<CartPreview> Preview(Guid tenant,Guid id,CartPreviewRequest request,bool guest,string? capability,CancellationToken ct);
 Task<PreparedCartView> Prepare(Guid tenant,Guid id,PrepareCartRequest request,bool guest,string? capability,CancellationToken ct);
 Task<PreparedCartView> ForOrder(Guid tenant,Guid id,PreparationProof proof,string? capability,CancellationToken ct);
}
