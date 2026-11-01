using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed partial class OrderService
{
 public async Task<Guid[]> ImportHistory(Guid tenant,HistoricalOrderImport request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Owner,ct);Id(request.Key);OrderRules.Text(request.Source,80);
  if(request.Orders is null||request.Orders.Length is <1 or >100||request.Orders.Select(o=>o.Reference).Distinct(StringComparer.Ordinal).Count()!=request.Orders.Length)throw new BusinessFault("InvalidHistoricalImport",400);
  // Unlinked source customers remain private historical references until a separate verified account-link flow exists.
  if(request.Orders.Any(o=>o.UserId is not null))throw new BusinessFault("HistoricalCustomerLinkRequiresVerification",400);
  var hash=Hash(request);return await repository.Transaction(async token=>
  {
   var previous=await repository.Import(request.Key,token);if(previous is not null){if(previous.Hash!=hash)throw new BusinessFault("HistoricalImportKeyReused");return ContractJson.Read<Guid[]>(previous.Result);}
   var ids=new List<Guid>();foreach(var row in request.Orders)
   {
    OrderRules.Text(row.Reference,120);OrderRules.Text(row.Number,80);if(!Enum.IsDefined(row.Status)||!Enum.IsDefined(row.PaymentState)||row.DecimalPlaces is <0 or >3||row.Currency.Length!=3||row.Currency.Any(c=>c is <'A' or >'Z')||row.CreatedAt>Now||row.Items is null||row.Items.Length is <1 or >50)throw new BusinessFault("InvalidHistoricalOrder",400);
    foreach(var amount in new[]{row.Subtotal,row.Discount,row.Net,row.Tax,row.Total})OrderRules.Money(amount);
    if(row.Subtotal-row.Discount!=row.Net||row.Net+row.Tax!=row.Total||row.Items.Sum(i=>i.Subtotal)!=row.Subtotal||row.Items.Sum(i=>i.Discount)!=row.Discount||row.Items.Sum(i=>i.Net)!=row.Net||row.Items.Sum(i=>i.Tax)!=row.Tax||row.Items.Sum(i=>i.Total)!=row.Total)throw new BusinessFault("HistoricalTotalsMismatch",400);
    var order=new SalesOrder{TenantId=tenant,Id=Guid.NewGuid(),Number=row.Number,Status=row.Status,PaymentState=row.PaymentState,PaymentMethod=OrderRules.Payment(row.PaymentMethod),Currency=row.Currency,DecimalPlaces=row.DecimalPlaces,Subtotal=row.Subtotal,Discount=row.Discount,Net=row.Net,Tax=row.Tax,ItemsTotal=row.Total,Total=row.Total,ContactDocument=ContractJson.Write(Contact(row.Contact)),Historical=true,SourceReference=row.Reference,CreatedAt=row.CreatedAt,UpdatedAt=Now};
    foreach(var line in row.Items)
    {
     Id(line.ProductId);Id(line.VariantId);OrderRules.Quantity(line.Quantity);OrderRules.Text(line.Sku,64);OrderRules.Text(line.Name);foreach(var amount in new[]{line.UnitPrice,line.Subtotal,line.Discount,line.Net,line.Tax,line.Total})OrderRules.Money(amount);
     if(line.Subtotal-line.Discount!=line.Net||line.Net+line.Tax!=line.Total||line.Fulfilled>line.Quantity||line.Delivered>line.Fulfilled||line.ReturnRequested!=0||line.Returned!=0)throw new BusinessFault("HistoricalLineMismatch",400);
     order.Items.Add(new OrderItem{TenantId=tenant,OrderId=order.Id,ProductId=line.ProductId,VariantId=line.VariantId,Sku=line.Sku,Name=line.Name,Quantity=line.Quantity,ShipmentRequested=line.Fulfilled,Fulfilled=line.Fulfilled,Delivered=line.Delivered,UnitPrice=line.UnitPrice,Subtotal=line.Subtotal,Discount=line.Discount,Net=line.Net,Tax=line.Tax,Total=line.Total});
    }
    order.PriceSnapshot=ContractJson.Write(row);OrderRules.Lines(order);repository.Add(order);repository.Add(new OrderStatusHistory{TenantId=tenant,OrderId=order.Id,Current=order.Status,Reason="Read-only source history import",CreatedAt=Now});ids.Add(order.Id);
   }
   repository.Add(new OrderImport{TenantId=tenant,Id=request.Key,Source=request.Source,Hash=hash,Result=ContractJson.Write(ids),CreatedAt=Now});await repository.Commit("order.history.imported",request.Key,null,token);return ids.ToArray();
  },ct);
 }
}
