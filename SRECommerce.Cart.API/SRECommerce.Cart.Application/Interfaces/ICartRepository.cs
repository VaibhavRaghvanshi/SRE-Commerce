using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Application;
public interface ICartRepository
{
 Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
 Task<ShoppingCart?> Cart(Guid id,CancellationToken ct);Task<ShoppingCart?> Customer(Guid user,CancellationToken ct);
 Task<CartSession?> Session(Guid cart,CancellationToken ct);Task<CartOperation?> Operation(Guid cart,Guid id,CancellationToken ct);
 Task<CheckoutPreparation?> Preparation(Guid id,CancellationToken ct);
 void Add<T>(T value)where T:class;void Version(ShoppingCart value,string expected);
 Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct);
}
