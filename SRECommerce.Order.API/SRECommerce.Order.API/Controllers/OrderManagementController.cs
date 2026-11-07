using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Order.Application;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.API;
[ApiController,Authorize,RequestSizeLimit(1048576),Route("api/v1/tenants/{tenant:guid}/order-management")]
public sealed class OrderManagementController(IOrderService service):ControllerBase
{
 [HttpGet("policy")]public async Task<IActionResult> Policy(Guid tenant,CancellationToken ct)=>Ok(await service.Policy(tenant,null,false,ct));
 [HttpPut("policy")]public async Task<IActionResult> Policy(Guid tenant,OrderPolicyRequest request,CancellationToken ct)=>Ok(await service.Policy(tenant,request,false,ct));
 [HttpGet("orders")]public async Task<IActionResult> List(Guid tenant,[FromQuery]int page=1,[FromQuery]OrderStatus? status=null,CancellationToken ct=default)=>Ok(await service.Orders(tenant,page,status,true,ct));
 [HttpGet("checkout-reviews")]public async Task<IActionResult> Reviews(Guid tenant,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Reviews(tenant,page,ct));
 [HttpPost("checkout-reviews/{id:guid}/recover")]public async Task<IActionResult> Recover(Guid tenant,Guid id,CheckoutRecoveryRequest request,CancellationToken ct)=>Accepted(await service.RecoverCheckout(tenant,id,request,ct));
 [HttpGet("orders/{id:guid}")]public async Task<IActionResult> Get(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Order(tenant,id,false,null,true,ct));
 [HttpGet("orders/{id:guid}/history")]public async Task<IActionResult> History(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.History(tenant,id,false,null,true,ct));
 [HttpPost("orders/{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid tenant,Guid id,CancelOrderRequest request,CancellationToken ct)=>Ok(await service.Cancel(tenant,id,request,false,null,true,ct));
 [HttpPost("orders/{id:guid}/status")]public async Task<IActionResult> Status(Guid tenant,Guid id,OrderStateRequest request,CancellationToken ct)=>Ok(await service.ChangeStatus(tenant,id,request,ct));
 [HttpPost("orders/{id:guid}/invoice-reference")]public async Task<IActionResult> Invoice(Guid tenant,Guid id,InvoiceRequest request,CancellationToken ct)=>Ok(await service.Invoice(tenant,id,request,ct));
 [HttpPost("orders/{id:guid}/gst-worksheet"),RequestSizeLimit(32768)]
 public async Task<IActionResult> GstWorksheet(Guid tenant,Guid id,GstWorksheetRequest request,CancellationToken ct)=>Ok(await service.PreviewGstWorksheet(tenant,id,request,ct));
 [HttpPost("orders/{id:guid}/fulfillments")]public async Task<IActionResult> Fulfill(Guid tenant,Guid id,FulfillOrderRequest request,CancellationToken ct)=>Ok(await service.Fulfill(tenant,id,request,ct));
 [HttpGet("orders/{id:guid}/fulfillments")]public async Task<IActionResult> Fulfillments(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Fulfillments(tenant,id,false,null,true,ct));
 [HttpGet("orders/{id:guid}/returns")]public async Task<IActionResult> Returns(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Returns(tenant,id,false,null,true,ct));
 [HttpPost("returns/{id:guid}/status")]public async Task<IActionResult> Return(Guid tenant,Guid id,ReturnStateRequest request,CancellationToken ct)=>Ok(await service.ChangeReturn(tenant,id,request,ct));
 [HttpGet("orders/{id:guid}/exchanges")]public async Task<IActionResult> Exchanges(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Exchanges(tenant,id,false,null,true,ct));
 [HttpPost("exchanges/{id:guid}/status")]public async Task<IActionResult> Exchange(Guid tenant,Guid id,ExchangeStateRequest request,CancellationToken ct)=>Ok(await service.ChangeExchange(tenant,id,request,ct));
 [HttpPost("historical-imports")]public async Task<IActionResult> Import(Guid tenant,HistoricalOrderImport request,CancellationToken ct)=>Ok(await service.ImportHistory(tenant,request,ct));
}
