using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Kernel;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed class OrderSaga(IOrderRepository repository,IOrderAccess access,IOrderDependencies dependencies,TimeProvider clock):IOrderSaga
{
 private DateTimeOffset Now=>clock.GetUtcNow();
 private async Task Save(Guid id,Guid lease,SagaPhase expected,SagaPhase next,Action<CheckoutIntent>? change,CancellationToken ct,TimeSpan? delay=null)
 {
  await repository.Transaction(async token=>
  {
   var intent=await repository.FreshIntent(id,token)??throw new BusinessFault("CheckoutNotFound",404);if(intent.LeaseId!=lease||intent.Phase!=expected)throw new BusinessFault("CheckoutLeaseLost");
   change?.Invoke(intent);intent.Phase=next;intent.LeaseId=null;intent.LeaseUntil=null;intent.NextAttemptAt=Now+(delay??TimeSpan.Zero);intent.Attempts=0;if(expected<SagaPhase.CompensatePayment)intent.LastFault=null;
   var action=next==SagaPhase.Done?(intent.State==CheckoutState.Accepted?"order.confirmed":intent.State==CheckoutState.Cancelled?"order.cancelled":"order.checkout.failed"):"order.checkout.progressed";
   if(next==SagaPhase.Done&&await repository.Order(id,token) is {Historical:false} order)
   {
    if(order.PaymentMethod!="CounterCash")
    {
     if(intent.State==CheckoutState.Accepted){repository.EmitNotice(0,id);if(order.PaymentState==PaymentState.Captured&&order.Total>0)repository.EmitNotice(1,id);}
     if(intent.State==CheckoutState.Cancelled)repository.EmitNotice(4,id);
    }
   }
   await repository.Commit(action,id,next==SagaPhase.Done?new{OrderId=id}:null,token);return true;
  },ct);
 }
 private async Task Fail(Guid id,Guid lease,SagaPhase expected,Exception error,CancellationToken ct)
 {
  await repository.Transaction(async token=>
  {
   var intent=await repository.FreshIntent(id,token);if(intent is null||intent.LeaseId!=lease||intent.Phase!=expected)return false;
   var permanent=error is BusinessFault fault&&fault.Status is 400 or 403 or 404 or 409 or 410;
   intent.Attempts++;intent.LastFault=error is BusinessFault known?known.Code:error is DependencyFault dependency?dependency.Dependency:"CheckoutDependencyFailure";
   intent.LeaseId=null;intent.LeaseUntil=null;intent.NextAttemptAt=Now.AddSeconds(Math.Min(60,Math.Pow(2,Math.Min(intent.Attempts,5))));
   if(permanent&&intent.State!=CheckoutState.Compensating){intent.State=CheckoutState.Compensating;intent.Phase=SagaPhase.CompensatePayment;intent.Attempts=0;}
   else if(intent.Attempts>=12){intent.State=CheckoutState.ManualReview;intent.NextAttemptAt=DateTimeOffset.MaxValue;if(await repository.Order(id,token) is {} order&&order.Status is not(OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Completed)){var previous=order.Status;order.Status=OrderStatus.ManualReview;order.Revision++;order.UpdatedAt=Now;repository.Add(new OrderStatusHistory{TenantId=order.TenantId,OrderId=id,Previous=previous,Current=order.Status,Reason="Checkout recovery needs store review",CreatedAt=Now});}}
   await repository.Commit("order.checkout.recovery",id,null,token);return true;
  },ct);
 }
 public async Task Run(Guid tenant,Guid id,string? cartCapability,CancellationToken ct,bool interactive=false)
 {
  var lease=Guid.NewGuid();var intent=await repository.Transaction(async token=>
  {
   var current=await repository.FreshIntent(id,token)??throw new BusinessFault("CheckoutNotFound",404);if(current.TenantId!=tenant)throw new BusinessFault("CheckoutNotFound",404);
   if(current.State is CheckoutState.Accepted or CheckoutState.Cancelled or CheckoutState.Failed or CheckoutState.ManualReview||current.LeaseUntil>Now||!interactive&&current.NextAttemptAt>Now)return null;
   current.LeaseId=lease;current.LeaseUntil=Now.AddSeconds(45);await repository.Commit("order.checkout.leased",id,null,token);return current;
  },ct);
  if(intent is null)return;var phase=intent.Phase;
  try
  {
   if(intent.State!=CheckoutState.Compensating&&intent.Deadline<=Now)throw new BusinessFault("CheckoutDeadlineExpired");
   await Step(intent,lease,cartCapability,interactive,ct);
  }
  catch(BusinessFault fault)when(fault.Code=="CheckoutLeaseLost"){}
  catch(DomainFault error){await Fail(id,lease,phase,new BusinessFault(error.Code,error.Status),ct);}
  catch(Exception error)when(error is BusinessFault or DependencyFault){await Fail(id,lease,phase,error,ct);}
 }
 public async Task Recover(CancellationToken ct)
 {foreach(var intent in await repository.Due(Now,ct))await Run(intent.TenantId,intent.Id,null,ct);}
 private static bool Same(OrderQuantity[] expected,OrderQuantity[] actual)=>expected.Length==actual.Length&&actual.Select(i=>i.VariantId).Distinct().Count()==actual.Length&&expected.All(e=>actual.Any(a=>a.VariantId==e.VariantId&&a.Quantity==e.Quantity));
 private void Status(SalesOrder order,OrderStatus target,string reason)
 {var previous=order.Status;OrderRules.Move(order,target);order.Revision++;order.UpdatedAt=Now;repository.Add(new OrderStatusHistory{TenantId=order.TenantId,OrderId=order.Id,Previous=previous,Current=target,Reason=reason,CreatedAt=Now});}
 private async Task Step(CheckoutIntent intent,Guid lease,string? capability,bool interactive,CancellationToken ct)
 {
  var tenant=intent.TenantId;var id=intent.Id;var phase=intent.Phase;var request=ContractJson.Read<CheckoutRequest>(intent.RequestDocument);
  switch(phase)
  {
   case SagaPhase.AwaitCartClaim:
   {
    var claim=interactive?await dependencies.ClaimCart(tenant,id,intent.PreparationId,intent.UserId,intent.CustomerRole,capability,ct):await dependencies.CartClaim(tenant,id,ct);
    if(claim is null){await Save(id,lease,phase,phase,null,ct,TimeSpan.FromSeconds(5));return;}
    if(claim.OrderId!=id||claim.Status!=0||claim.ExpiresAt<=Now||claim.Preparation.Id!=intent.PreparationId||claim.Preparation.TenantId!=tenant||claim.Preparation.UserId!=intent.UserId||claim.Preparation.CustomerRole!=intent.CustomerRole)throw new BusinessFault("CartClaimRejected");
    await Save(id,lease,phase,SagaPhase.Validate,value=>{value.CartClaimDocument=ContractJson.Write(claim);value.State=CheckoutState.Processing;value.Deadline=claim.Preparation.ExpiresAt;},ct);return;
   }
   case SagaPhase.Validate:
   {
    await access.RequireOrdering(tenant,ct);var claim=ContractJson.Read<SnapshotCartClaim>(intent.CartClaimDocument);var preparation=claim.Preparation;var quote=preparation.Quote;var quantities=preparation.Items.Select(i=>new OrderQuantity(i.VariantId,i.Quantity)).ToArray();
    if(quote.TenantId!=tenant||quote.UserId!=intent.UserId||quote.ExpiresAt<=Now||quote.Currency.Length!=3||quote.DecimalPlaces is <0 or >3||quantities.Length is <1 or >50||!Same(quantities,quote.Calculation.Lines.Select(l=>new OrderQuantity(l.VariantId,l.Quantity)).ToArray()))throw new BusinessFault("QuoteSnapshotMismatch");
    var products=await dependencies.Products(tenant,quantities.Select(i=>i.VariantId).ToArray(),ct);
    foreach(var item in preparation.Items){var product=products.SingleOrDefault(p=>p.Id==item.ProductId&&p.Variants.Any(v=>v.Id==item.VariantId&&v.Active))??throw new BusinessFault("OrderProductUnavailable");if(product.B2BOnly&&intent.CustomerRole is not("Owner" or "Manager" or "B2BBuyer"))throw new BusinessFault("OrderProductUnavailable");}
    var shipping=await dependencies.ShippingQuote(tenant,intent.ShippingQuoteId,ct);
    if(shipping.Id!=intent.ShippingQuoteId||shipping.TenantId!=tenant||shipping.PreparationId!=preparation.Id||shipping.UserId!=intent.UserId||shipping.ExpiresAt<=Now||shipping.Currency!=quote.Currency||shipping.CountryCode!=request.Contact.CountryCode||shipping.PostalCode!=request.Contact.PostalCode||!Same(quantities,shipping.Items)||shipping.Net+shipping.Tax!=shipping.Total)throw new BusinessFault("ShippingQuoteMismatch");
    var calculation=quote.Calculation;foreach(var amount in new[]{calculation.Subtotal,calculation.Discount,calculation.Net,calculation.Tax,calculation.Total,shipping.Net,shipping.Tax,shipping.Total})OrderRules.Money(amount);
    if(calculation.Subtotal-calculation.Discount!=calculation.Total||calculation.Net+calculation.Tax!=calculation.Total||calculation.Lines.Sum(l=>l.Subtotal)!=calculation.Subtotal||calculation.Lines.Sum(l=>l.Discount)!=calculation.Discount||calculation.Lines.Sum(l=>l.Net)!=calculation.Net||calculation.Lines.Sum(l=>l.Tax)!=calculation.Tax||calculation.Lines.Sum(l=>l.Total)!=calculation.Total)throw new BusinessFault("QuoteTotalsMismatch");
    var policy=await repository.Policy(ct)??throw new BusinessFault("OrderingNotConfigured");var total=calculation.Total+shipping.Total;OrderRules.Money(total);if(total>policy.MaximumTotal)throw new BusinessFault("OrderMaximumExceeded");
    if(request.PaymentMethod=="CounterCash"&&(shipping.PickupWarehouseId is null||shipping.PickupWarehouseId==Guid.Empty||request.CounterCashReceived!=total))
     throw new BusinessFault("CounterPickupAndExactCashRequired");
    var order=new SalesOrder{TenantId=tenant,Id=id,UserId=intent.UserId,CartId=preparation.CartId,CartRevision=preparation.CartRevision,PreparationId=preparation.Id,QuoteId=quote.Id,ShippingQuoteId=shipping.Id,Number="SRE-"+id.ToString("N").ToUpperInvariant(),PaymentMethod=request.PaymentMethod,Currency=quote.Currency,DecimalPlaces=quote.DecimalPlaces,Subtotal=calculation.Subtotal,Discount=calculation.Discount,Net=calculation.Net,Tax=calculation.Tax,ItemsTotal=calculation.Total,ShippingTotal=shipping.Total,Total=total,ContactDocument=ContractJson.Write(request.Contact),PriceSnapshot=ContractJson.Write(quote),ShippingSnapshot=ContractJson.Write(shipping),CreatedAt=Now,UpdatedAt=Now};
    foreach(var line in calculation.Lines){var item=preparation.Items.Single(i=>i.VariantId==line.VariantId);order.Items.Add(new OrderItem{TenantId=tenant,OrderId=id,ProductId=item.ProductId,VariantId=item.VariantId,Sku=item.Sku,Name=item.Name,Quantity=line.Quantity,UnitPrice=line.UnitPrice,Subtotal=line.Subtotal,Discount=line.Discount,Net=line.Net,Tax=line.Tax,Total=line.Total});}OrderRules.Lines(order);
    await Save(id,lease,phase,SagaPhase.ClaimPrice,_=>{repository.Add(order);repository.Add(new OrderStatusHistory{TenantId=tenant,OrderId=id,Current=order.Status,Reason="Server snapshots validated",CreatedAt=Now});},ct);return;
   }
   case SagaPhase.ClaimPrice:
   {
    var order=await Order(id,ct);var result=await dependencies.ClaimPrice(tenant,id,order.QuoteId!.Value,intent.UserId,ct);var cart=ContractJson.Read<SnapshotCartClaim>(intent.CartClaimDocument);
    if(result.OrderId!=id||result.QuoteId!=order.QuoteId||result.Status!=0||result.ExpiresAt<=Now)throw new BusinessFault("PriceClaimUnavailable");
    await Save(id,lease,phase,SagaPhase.PlanStock,value=>value.Deadline=result.ExpiresAt<cart.ExpiresAt?result.ExpiresAt:cart.ExpiresAt,ct);return;
   }
   case SagaPhase.PlanStock:
   {
    var order=await Order(id,ct);var expiry=Now.AddMinutes(10);if(expiry>intent.Deadline.AddSeconds(-10))expiry=intent.Deadline.AddSeconds(-10);if(expiry<Now.AddSeconds(30))throw new BusinessFault("StockHoldWindowExpired");
    var warehouses=await dependencies.Warehouses(tenant,order.Items.Select(i=>i.VariantId).ToArray(),expiry,ct);var shipping=ContractJson.Read<ShippingQuoteSnapshot>(order.ShippingSnapshot);if(shipping.PickupWarehouseId is {} pickup)warehouses=warehouses.Where(w=>w.WarehouseId==pickup).ToArray();
    var remaining=order.Items.ToDictionary(i=>i.VariantId,i=>i.Quantity);var plans=new List<OrderReservation>();
    foreach(var warehouse in warehouses.OrderBy(w=>w.Code,StringComparer.Ordinal).ThenBy(w=>w.WarehouseId))
    {var items=new List<OrderQuantity>();foreach(var item in remaining.ToArray()){var stock=warehouse.Items.SingleOrDefault(i=>i.VariantId==item.Key&&i.Tracked);var quantity=Math.Min(item.Value,stock?.Available??0);if(quantity>0){items.Add(new(item.Key,quantity));remaining[item.Key]-=quantity;}}if(items.Count>0)plans.Add(new OrderReservation{TenantId=tenant,Id=OrderRules.Operation(id,"warehouse:"+warehouse.WarehouseId.ToString("N")),OrderId=id,WarehouseId=warehouse.WarehouseId,Items=ContractJson.Write(items),ExpiresAt=expiry});if(remaining.Values.All(q=>q==0))break;}
    if(remaining.Values.Any(q=>q>0)||plans.Count is <1 or >5)throw new BusinessFault("OrderStockUnavailable");
    await Save(id,lease,phase,SagaPhase.ReserveStock,value=>{foreach(var plan in plans)repository.Add(plan);value.Deadline=expiry;},ct);return;
   }
   case SagaPhase.ReserveStock:
   {
    var plan=(await repository.Reservations(id,ct)).FirstOrDefault(r=>r.Status==ReservationStep.Planned);
    if(plan is null){await Save(id,lease,phase,SagaPhase.StartPayment,null,ct);return;}
    var items=ContractJson.Read<OrderQuantity[]>(plan.Items);var result=await dependencies.Reserve(tenant,plan.Id,plan.WarehouseId,"Order "+id.ToString("N"),plan.ExpiresAt,items,ct);
    if(result.Id!=plan.Id||result.WarehouseId!=plan.WarehouseId||result.Status!=0||result.ExpiresAt<=Now||!Same(items,result.Items.GroupBy(i=>i.VariantId).Select(g=>new OrderQuantity(g.Key,g.Sum(i=>i.Quantity))).ToArray()))throw new BusinessFault("StockReservationRejected");
    await Save(id,lease,phase,phase,_=>{plan.Status=ReservationStep.Held;plan.RemoteVersion=result.Version;},ct);return;
   }
   case SagaPhase.StartPayment:
   {
    await access.RequireOrdering(tenant,ct);var order=await Order(id,ct);var result=await dependencies.StartPayment(tenant,id,ct);PaymentMatches(order,result);
    if(result.Status is not(0 or 1 or 2 or 7))throw new BusinessFault("PaymentDeclined");
    await Save(id,lease,phase,SagaPhase.WaitPayment,_=>order.PaymentState=PaymentStatus(result),ct);return;
   }
   case SagaPhase.WaitPayment:
   {
    var order=await Order(id,ct);var payment=await dependencies.Payment(tenant,id,ct)??throw new BusinessFault("PaymentNotFound",404);PaymentMatches(order,payment);
    if(payment.Status==0){await Save(id,lease,phase,phase,null,ct,TimeSpan.FromSeconds(3));return;}
    if(payment.Status is not(1 or 2 or 7))throw new BusinessFault("PaymentDeclined");
    await Save(id,lease,phase,payment.Status==1?SagaPhase.CapturePayment:SagaPhase.CommitStock,_=>order.PaymentState=PaymentStatus(payment),ct);return;
   }
   case SagaPhase.CapturePayment:
   {
    await access.RequireOrdering(tenant,ct);var order=await Order(id,ct);var payment=await dependencies.Payment(tenant,id,ct)??throw new BusinessFault("PaymentNotFound",404);if(payment.Status==1)payment=await dependencies.CapturePayment(tenant,id,payment.Version,ct);PaymentMatches(order,payment);if(payment.Status!=2)throw new BusinessFault("PaymentCaptureUnavailable");
    await Save(id,lease,phase,SagaPhase.CommitStock,_=>order.PaymentState=PaymentState.Captured,ct);return;
   }
   case SagaPhase.CommitStock:
   {
    var plan=(await repository.Reservations(id,ct)).FirstOrDefault(r=>r.Status==ReservationStep.Held);if(plan is null){await Save(id,lease,phase,SagaPhase.RedeemPrice,null,ct);return;}
    var reservation=await dependencies.Reservation(tenant,plan.Id,ct)??throw new BusinessFault("StockReservationNotFound",404);if(reservation.Status==0)reservation=await dependencies.CloseStock(tenant,plan.Id,true,reservation.Version,ct);if(reservation.Status!=1)throw new BusinessFault("StockCommitUnavailable");
    await Save(id,lease,phase,phase,_=>{plan.Status=ReservationStep.Committed;plan.RemoteVersion=reservation.Version;},ct);return;
   }
   case SagaPhase.RedeemPrice:
   {
    var redemption=await dependencies.Redemption(tenant,id,ct)??throw new BusinessFault("PriceClaimNotFound",404);if(redemption.Status==0)redemption=await dependencies.ClosePrice(tenant,id,true,redemption.Version,ct);if(redemption.Status!=1)throw new BusinessFault("PriceClaimExpired");await Save(id,lease,phase,SagaPhase.CompleteCart,null,ct);return;
   }
   case SagaPhase.CompleteCart:
   {
    var claim=await dependencies.CartClaim(tenant,id,ct)??throw new BusinessFault("CartClaimNotFound",404);if(claim.Status==0)claim=await dependencies.CloseCart(tenant,id,true,claim.Version,ct);if(claim.Status!=1)throw new BusinessFault("CartClaimExpired");await Save(id,lease,phase,SagaPhase.Confirm,null,ct);return;
   }
   case SagaPhase.Confirm:
   {
    await access.RequireOrdering(tenant,ct);var order=await Order(id,ct);await Save(id,lease,phase,SagaPhase.Done,value=>{Status(order,OrderStatus.Confirmed,"Authoritative checkout completed");value.State=CheckoutState.Accepted;},ct);
    return;
   }
   case SagaPhase.CompensatePayment:
   {
    if(await repository.Order(id,ct) is {} order){var payment=await dependencies.AbortPayment(tenant,id,ct);if(payment.Status is 2 or 5){var outstanding=payment.Amount-payment.Refunded;if(outstanding>0)await dependencies.Refund(tenant,id,OrderRules.Operation(id,"checkout-refund"),outstanding,"Checkout cancellation or failure",ct);payment=await dependencies.Payment(tenant,id,ct)??throw new DependencyFault("Payment");if(payment.Amount>payment.Refunded&&payment.Status is 2 or 5)throw new DependencyFault("PaymentRefund");}await Save(id,lease,phase,SagaPhase.CompensateStock,_=>{order.PaymentState=PaymentStatus(payment);order.Refunded=payment.Refunded;},ct);}else await Save(id,lease,phase,SagaPhase.CompensateStock,null,ct);return;
   }
   case SagaPhase.CompensateStock:
   {
    var plan=(await repository.Reservations(id,ct)).FirstOrDefault(r=>r.Status is ReservationStep.Planned or ReservationStep.Held or ReservationStep.Committed);if(plan is null){await Save(id,lease,phase,SagaPhase.ReleasePrice,null,ct);return;}
    await dependencies.AbortStock(tenant,plan.Id,OrderRules.Operation(plan.Id,"checkout-restore"),ct);var final=await dependencies.Reservation(tenant,plan.Id,ct);if(final is not null&&final.Status is not(2 or 3 or 4))throw new DependencyFault("StockCompensation");await Save(id,lease,phase,phase,_=>plan.Status=final?.Status==4?ReservationStep.Restored:ReservationStep.Released,ct);return;
   }
   case SagaPhase.ReleasePrice:
   {
    if(await repository.Order(id,ct) is {} order)await dependencies.AbortPrice(tenant,id,order.QuoteId!.Value,intent.UserId,ct);await Save(id,lease,phase,SagaPhase.ReleaseCart,null,ct);return;
   }
   case SagaPhase.ReleaseCart:
   {
    await dependencies.AbortCart(tenant,id,intent.PreparationId,intent.UserId,intent.CustomerRole,ct);var order=await repository.Order(id,ct);
    await Save(id,lease,phase,SagaPhase.Done,value=>{value.State=value.CancellationReason is null?CheckoutState.Failed:CheckoutState.Cancelled;if(order is not null){var target=value.CancellationReason is null?OrderStatus.Failed:OrderStatus.Cancelled;Status(order,target,value.CancellationReason??"Checkout compensated after rejection");}},ct);return;
   }
   case SagaPhase.Done:return;
   default:throw new BusinessFault("UnknownCheckoutPhase",500);
  }
 }
 private async Task<SalesOrder> Order(Guid id,CancellationToken ct)=>await repository.Order(id,ct)??throw new BusinessFault("OrderNotFound",404);
 private static void PaymentMatches(SalesOrder order,RemotePayment payment){if(payment.OrderId!=order.Id||payment.Currency!=order.Currency||payment.Amount!=order.Total||payment.Method!=order.PaymentMethod||payment.Refunded<0||payment.Refunded>payment.Amount)throw new BusinessFault("PaymentSnapshotMismatch");}
 private static PaymentState PaymentStatus(RemotePayment payment)=>payment.Status switch{0=>PaymentState.Pending,1=>PaymentState.Authorized,2=>PaymentState.Captured,5=>PaymentState.PartiallyRefunded,6=>PaymentState.Refunded,7=>PaymentState.CashOnDelivery,_=>PaymentState.Failed};
}
