using Microsoft.AspNetCore.Http;using Microsoft.EntityFrameworkCore;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Application;using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Infrastructure;
public sealed class CartRepository(CartDbContext db,ModuleScope scope,IHttpContextAccessor contexts,ServiceSpec service):ICartRepository
{
 public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct){await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var result=await action(ct);await tx.CommitAsync(ct);return result;}
 public Task<ShoppingCart?> Cart(Guid id,CancellationToken ct)=>db.Carts.Include(c=>c.Items).SingleOrDefaultAsync(c=>c.Id==id,ct);
 public Task<ShoppingCart?> Customer(Guid user,CancellationToken ct)=>db.Carts.Include(c=>c.Items).SingleOrDefaultAsync(c=>c.UserId==user&&c.Status==CartStatus.Active,ct);
 public Task<CartSession?> Session(Guid id,CancellationToken ct)=>db.Sessions.SingleOrDefaultAsync(s=>s.CartId==id,ct);
 public Task<CartOperation?> Operation(Guid cart,Guid id,CancellationToken ct)=>db.Operations.SingleOrDefaultAsync(o=>o.CartId==cart&&o.Id==id,ct);
 public Task<CheckoutPreparation?> Preparation(Guid id,CancellationToken ct)=>db.Preparations.SingleOrDefaultAsync(p=>p.Id==id,ct);
 public void Add<T>(T value)where T:class=>db.Add(value);public void Version(ShoppingCart value,string expected)=>db.ExpectedVersion(value,expected);
 public async Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct){db.Record(scope,contexts,action,resource.ToString());if(integrationEvent is not null)db.Emit(scope,service,contexts,action+".v1",integrationEvent);await db.SaveChangesAsync(ct);}
}
