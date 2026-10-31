using System.Security.Cryptography;using System.Text;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Application;
public sealed class CartService(ICartRepository repository,ICartAccess access,TimeProvider clock):ICartService
{
 private DateTimeOffset Now=>clock.GetUtcNow();
 private static void Id(Guid id){if(id==Guid.Empty)throw new BusinessFault("InvalidCartId",400);}
 private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
 private static CartView View(ShoppingCart c)=>new(c.Id,c.TenantId,c.UserId is null,c.Status,c.Revision,c.ExpiresAt,c.Items.Where(i=>i.Active).OrderBy(i=>i.Sku).Select(i=>new CartLineView(i.ProductId,i.VariantId,i.Sku,i.Name,i.Slug,i.Quantity)).ToArray(),Convert.ToBase64String(c.Version));
 private void Active(ShoppingCart cart){if(cart.Status!=CartStatus.Active)throw new BusinessFault("CartClosed");if(cart.ExpiresAt<=Now)throw new BusinessFault("CartExpired",410);}
 private async Task<ShoppingCart> Owned(Guid id,bool guest,string? capability,CancellationToken ct,bool mergedReplay=false)
 {
  var cart=await repository.Cart(id,ct)??throw new BusinessFault("CartNotFound",404);
  if(guest)
  {
   if(cart.UserId is not null)throw new BusinessFault("CartNotFound",404);var session=await repository.Session(id,ct)??throw new BusinessFault("CartNotFound",404);CartRules.Match(session.CapabilityHash,capability);
   if(session.ExpiresAt<=Now)throw new BusinessFault("CartExpired",410);if(session.RevokedAt is not null&&!mergedReplay)throw new BusinessFault("CartClosed");
  }
  else if(cart.UserId!=access.Actor?.UserId)throw new BusinessFault("CartNotFound",404);
  if(!mergedReplay)Active(cart);return cart;
 }
 private async Task Touch(ShoppingCart cart,CancellationToken ct)
 {
  cart.Revision++;cart.UpdatedAt=Now;cart.ExpiresAt=Now.AddDays(cart.UserId is null?7:30);
  if(cart.UserId is null){var session=await repository.Session(cart.Id,ct)??throw new BusinessFault("CartSessionMissing",500);session.ExpiresAt=cart.ExpiresAt;}
 }
 private async Task<CartView?> Replay(ShoppingCart cart,Guid id,string hash,CancellationToken ct)
 {Id(id);var prior=await repository.Operation(cart.Id,id,ct);if(prior is null)return null;if(prior.Hash!=hash)throw new BusinessFault("CartOperationKeyReused");return ContractJson.Read<CartView>(prior.Result);}
 private async Task<CartView> Record(ShoppingCart cart,Guid operation,string hash,string action,CancellationToken ct)
 {
  await Touch(cart,ct);await repository.Commit(action,cart.Id,new{CartId=cart.Id,cart.Revision},ct);var view=View(cart);
  repository.Add(new CartOperation{TenantId=cart.TenantId,CartId=cart.Id,Id=operation,Hash=hash,Result=ContractJson.Write(view),CreatedAt=Now});await repository.Commit("cart.operation.recorded",operation,null,ct);return view;
 }
 private static void Set(ShoppingCart cart,CartCatalogProduct product,CartCatalogVariant sku,decimal quantity)
 {
  CartRules.Quantity(quantity);var row=cart.Items.SingleOrDefault(i=>i.VariantId==sku.Id);
  if(row is null){row=new CartItem{TenantId=cart.TenantId,CartId=cart.Id,VariantId=sku.Id};cart.Items.Add(row);}
  row.ProductId=product.Id;row.Sku=sku.Sku;row.Name=product.Name;row.Slug=product.Slug;row.Quantity=quantity;row.Active=true;
  if(cart.Items.Count(i=>i.Active)>50)throw new BusinessFault("CartItemLimit",400);
 }
 private async Task<CartCatalogProduct[]> Current(ShoppingCart cart,bool guest,CancellationToken ct)
 {
  var ids=cart.Items.Where(i=>i.Active).Select(i=>i.VariantId).ToArray();if(ids.Length==0)throw new BusinessFault("CartEmpty",400);
  var products=await access.Products(cart.TenantId,ids,guest,ct);foreach(var id in ids)if(!products.Any(p=>p.Variants.Any(v=>v.Id==id&&v.Active)))throw new BusinessFault("CartProductUnavailable");return products;
 }
 public async Task<CartView> Create(Guid tenant,CreateCartRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ct);Id(request.Id);var digest=guest?CartRules.Capability(capability):null;
  return await repository.Transaction(async token=>
  {
   if(await repository.Cart(request.Id,token) is not null)return View(await Owned(request.Id,guest,capability,token));
   Guid? user=guest?null:access.Actor?.UserId??throw new BusinessFault("UserRequired",401);
   if(user is {} userId&&await repository.Customer(userId,token) is {} existing)
   {
    if(existing.ExpiresAt>Now)return View(existing);
    existing.Status=CartStatus.Expired;await repository.Commit("cart.expired",existing.Id,null,token);
   }
   var value=new ShoppingCart{TenantId=tenant,Id=request.Id,UserId=user,CreatedAt=Now,UpdatedAt=Now,ExpiresAt=Now.AddDays(guest?7:30)};repository.Add(value);
   if(guest)repository.Add(new CartSession{TenantId=tenant,CartId=value.Id,CapabilityHash=digest!,ExpiresAt=value.ExpiresAt});
   await repository.Commit("cart.created",value.Id,new{CartId=value.Id,Guest=guest},token);return View(value);
  },ct);
 }
 public async Task<CartView> Get(Guid tenant,Guid id,bool guest,string? capability,CancellationToken ct)
 {await access.Authorize(tenant,guest,ct);return View(await Owned(id,guest,capability,ct));}
 public async Task<CartView> Item(Guid tenant,Guid id,CartItemRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ct);Id(request.VariantId);CartRules.Quantity(request.Quantity);var hash=Hash(new{Action="set",request});
  return await repository.Transaction(async token=>
  {
   var cart=await Owned(id,guest,capability,token);var prior=await Replay(cart,request.OperationId,hash,token);if(prior is not null)return prior;repository.Version(cart,request.Version);
   var products=await access.Products(tenant,[request.VariantId],guest,token);var product=products.SingleOrDefault(p=>p.Variants.Any(v=>v.Id==request.VariantId&&v.Active))??throw new BusinessFault("CartProductUnavailable");
   Set(cart,product,product.Variants.Single(v=>v.Id==request.VariantId),request.Quantity);return await Record(cart,request.OperationId,hash,"cart.item.saved",token);
  },ct);
 }
 public async Task<CartView> Remove(Guid tenant,Guid id,Guid variant,RemoveCartItemRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ct);Id(variant);var hash=Hash(new{Action="remove",Variant=variant,request});
  return await repository.Transaction(async token=>
  {
   var cart=await Owned(id,guest,capability,token);var prior=await Replay(cart,request.OperationId,hash,token);if(prior is not null)return prior;repository.Version(cart,request.Version);
   var row=cart.Items.SingleOrDefault(i=>i.VariantId==variant&&i.Active)??throw new BusinessFault("CartItemNotFound",404);row.Active=false;return await Record(cart,request.OperationId,hash,"cart.item.removed",token);
  },ct);
 }
 public async Task<CartView> Merge(Guid tenant,Guid id,MergeCartRequest request,string? guestCapability,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ct);Id(request.GuestCartId);var hash=Hash(new{Action="merge",request,Role=access.Actor?.TenantRole});
  return await repository.Transaction(async token=>
  {
   var target=await Owned(id,false,null,token);var source=await Owned(request.GuestCartId,true,guestCapability,token,true);var prior=await Replay(target,request.OperationId,hash,token);if(prior is not null)return prior;
   Active(source);repository.Version(target,request.Version);repository.Version(source,request.GuestVersion);
   var ids=source.Items.Where(i=>i.Active).Select(i=>i.VariantId).Concat(target.Items.Where(i=>i.Active).Select(i=>i.VariantId)).Distinct().ToArray();if(ids.Length>50)throw new BusinessFault("CartItemLimit",400);
   var products=ids.Length==0?[]:await access.Products(tenant,ids,false,token);
   foreach(var variant in ids)if(!products.Any(p=>p.Variants.Any(v=>v.Id==variant&&v.Active)))throw new BusinessFault("CartProductUnavailable");
   foreach(var row in source.Items.Where(i=>i.Active))
   {var product=products.Single(p=>p.Variants.Any(v=>v.Id==row.VariantId));var quantity=(target.Items.SingleOrDefault(i=>i.VariantId==row.VariantId&&i.Active)?.Quantity??0)+row.Quantity;Set(target,product,product.Variants.Single(v=>v.Id==row.VariantId),quantity);}
   source.Status=CartStatus.Merged;source.MergedIntoId=target.Id;source.Revision++;source.UpdatedAt=Now;var session=await repository.Session(source.Id,token)??throw new BusinessFault("CartSessionMissing",500);session.RevokedAt=Now;
   return await Record(target,request.OperationId,hash,"cart.merged",token);
  },ct);
 }
 private async Task<CartPreview> PreviewCore(ShoppingCart cart,Guid quoteId,string? coupon,bool guest,CancellationToken ct)
 {
  Id(quoteId);await Current(cart,guest,ct);var view=View(cart);var ids=view.Items.Select(i=>i.VariantId).ToArray();var stock=await access.Availability(cart.TenantId,ids,guest,ct);
  var quote=await access.Quote(cart.TenantId,quoteId,view.Items.Select(i=>new CartQuoteItem(i.VariantId,i.Quantity)).ToArray(),coupon,guest,ct);
  if(quote.Id!=quoteId||quote.TenantId!=cart.TenantId||quote.UserId!=cart.UserId||quote.ExpiresAt<=Now||quote.Calculation.Lines.Length!=view.Items.Length||quote.Calculation.Lines.Any(line=>!view.Items.Any(i=>i.VariantId==line.VariantId&&i.Quantity==line.Quantity)))throw new DependencyFault("Pricing");
  var ready=view.Items.All(i=>stock.Any(s=>s.VariantId==i.VariantId&&s.Tracked&&s.Available>=i.Quantity));return new(view,quote,stock,ready);
 }
 public async Task<CartPreview> Preview(Guid tenant,Guid id,CartPreviewRequest request,bool guest,string? capability,CancellationToken ct)
 {await access.Authorize(tenant,guest,ct);var cart=await Owned(id,guest,capability,ct);return await PreviewCore(cart,request.Id,request.CouponCode,guest,ct);}
 public async Task<PreparedCartView> Prepare(Guid tenant,Guid id,PrepareCartRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ct);Id(request.Id);var hash=Hash(new{CartId=id,request,Role=access.Actor?.TenantRole});
  return await repository.Transaction(async token=>
  {
   var cart=await Owned(id,guest,capability,token);var prior=await repository.Preparation(request.Id,token);
   if(prior is not null){if(prior.CartId!=id||prior.Hash!=hash)throw new BusinessFault("PreparationKeyReused");if(prior.ExpiresAt<=Now||prior.CartRevision!=cart.Revision)throw new BusinessFault("PreparationExpiredOrCartChanged");return ContractJson.Read<PreparedCartView>(prior.Document);}
   repository.Version(cart,request.Version);var preview=await PreviewCore(cart,request.Id,request.CouponCode,guest,token);if(!preview.CanPrepare)throw new BusinessFault("CartStockUnavailable");
   var result=new PreparedCartView(request.Id,id,tenant,cart.UserId,cart.Revision,preview.Quote.ExpiresAt,preview.Cart.Items,preview.Quote);var document=ContractJson.Write(result);
   if(Encoding.UTF8.GetByteCount(document)>131072)throw new BusinessFault("PreparationTooLarge",400);
   repository.Add(new CheckoutPreparation{TenantId=tenant,Id=request.Id,CartId=id,UserId=cart.UserId,CartRevision=cart.Revision,QuoteId=preview.Quote.Id,Hash=hash,Document=document,CreatedAt=Now,ExpiresAt=result.ExpiresAt});
   await repository.Commit("cart.checkout.prepared",request.Id,new{PreparationId=request.Id,CartId=id,QuoteId=preview.Quote.Id},token);return result;
  },ct);
 }
 public async Task<PreparedCartView> ForOrder(Guid tenant,Guid id,PreparationProof proof,string? capability,CancellationToken ct)
 {
  await access.Machine(tenant,ct);var preparation=await repository.Preparation(id,ct)??throw new BusinessFault("PreparationNotFound",404);var cart=await repository.Cart(preparation.CartId,ct)??throw new BusinessFault("CartNotFound",404);Active(cart);
  if(preparation.UserId!=proof.UserId||cart.UserId!=proof.UserId)throw new BusinessFault("PreparationNotFound",404);
  if(proof.UserId is null){var session=await repository.Session(cart.Id,ct)??throw new BusinessFault("PreparationNotFound",404);CartRules.Match(session.CapabilityHash,capability);if(session.RevokedAt is not null||session.ExpiresAt<=Now)throw new BusinessFault("CartClosed");}
  if(preparation.ExpiresAt<=Now||preparation.CartRevision!=cart.Revision)throw new BusinessFault("PreparationExpiredOrCartChanged");return ContractJson.Read<PreparedCartView>(preparation.Document);
 }
}
