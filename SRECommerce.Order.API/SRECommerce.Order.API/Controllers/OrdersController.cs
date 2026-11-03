using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Order.Application;
 namespace SRECommerce.Order.API;
 [ApiController,Authorize,RequestSizeLimit(131072),Route("api/v1/tenants/{tenant:guid}/orders")]
 public sealed class OrdersController(IOrderService service):ControllerBase
 {
  private string? Key=>Request.Headers["X-Order-Key"].ToString();
  [HttpPost("checkout")]public async Task<IActionResult> Checkout(Guid tenant,CheckoutRequest request,CancellationToken ct)=>StatusCode(202,await service.Checkout(tenant,request,false,Request.Headers["X-Cart-Key"].ToString(),Key,ct));
  [HttpGet("checkouts/{id:guid}")]public async Task<IActionResult> Checkout(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.CheckoutStatus(tenant,id,false,Key,ct));
  [HttpPost("checkouts/{id:guid}/cancel")]public async Task<IActionResult> CancelCheckout(Guid tenant,Guid id,CancelCheckoutRequest request,CancellationToken ct)=>Accepted(await service.CancelCheckout(tenant,id,request,false,Key,ct));
  [HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Order(tenant,id,false,Key,false,ct));
  [HttpGet("{id:guid}/history")]public async Task<IActionResult> History(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.History(tenant,id,false,Key,false,ct));
  [HttpPost("{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid tenant,Guid id,CancelOrderRequest request,CancellationToken ct)=>Ok(await service.Cancel(tenant,id,request,false,Key,false,ct));
  [HttpGet("{id:guid}/fulfillments")]public async Task<IActionResult> Fulfillments(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Fulfillments(tenant,id,false,Key,false,ct));
  [HttpPost("{id:guid}/returns")]public async Task<IActionResult> Return(Guid tenant,Guid id,ReturnOrderRequest request,CancellationToken ct)=>Ok(await service.RequestReturn(tenant,id,request,false,Key,ct));
  [HttpGet("{id:guid}/returns")]public async Task<IActionResult> Returns(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Returns(tenant,id,false,Key,false,ct));
  [HttpPost("{id:guid}/exchanges")]public async Task<IActionResult> Exchange(Guid tenant,Guid id,ExchangeOrderRequest request,CancellationToken ct)=>Ok(await service.RequestExchange(tenant,id,request,false,Key,ct));
  [HttpGet("{id:guid}/exchanges")]public async Task<IActionResult> Exchanges(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Exchanges(tenant,id,false,Key,false,ct));
  [HttpGet]public async Task<IActionResult> List(Guid tenant,[FromQuery]int page=1,[FromQuery]SRECommerce.Order.Domain.OrderStatus? status=null,CancellationToken ct=default)=>Ok(await service.Orders(tenant,page,status,false,ct));
 }
