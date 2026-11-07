using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Cart.Application;
namespace SRECommerce.Cart.API;
[ApiController,Authorize,RequestSizeLimit(65536),Route("api/v1/tenants/{tenant:guid}/cart/carts")]
public sealed class CartsController(ICartService service,IConfiguration configuration):ControllerBase
{
 private string? Capability=>Request.Headers["X-Cart-Key"].ToString();
 [HttpPost]public async Task<IActionResult> Create(Guid tenant,CreateCartRequest request,CancellationToken ct)=>Ok(await service.Create(tenant,request,false,Capability,ct));
 [HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Get(tenant,id,false,Capability,ct));
 [HttpGet("recovery")]public async Task<IActionResult> Recovery(Guid tenant,CancellationToken ct)
 {
  var candidate=await service.RecoveryCandidate(tenant,ct);
  return candidate is null?NoContent():Ok(candidate);
 }
 [HttpPost("{id:guid}/recover")]public async Task<IActionResult> Recover(Guid tenant,Guid id,RecoverCartRequest request,CancellationToken ct)=>Ok(await service.Recover(tenant,id,request,ct));
 [HttpGet("{id:guid}/reminder")]public async Task<IActionResult> Reminder(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Reminder(tenant,id,configuration.GetValue<bool>("CartReminders:Enabled"),ct));
 [HttpPut("{id:guid}/reminder")]public async Task<IActionResult> Reminder(Guid tenant,Guid id,CartReminderRequest request,CancellationToken ct)=>Ok(await service.SetReminder(tenant,id,request,configuration.GetValue<bool>("CartReminders:Enabled"),ct));
 [HttpPut("{id:guid}/items")]public async Task<IActionResult> Item(Guid tenant,Guid id,CartItemRequest request,CancellationToken ct)=>Ok(await service.Item(tenant,id,request,false,Capability,ct));
 [HttpPost("{id:guid}/items/{variant:guid}/remove")]public async Task<IActionResult> Remove(Guid tenant,Guid id,Guid variant,RemoveCartItemRequest request,CancellationToken ct)=>Ok(await service.Remove(tenant,id,variant,request,false,Capability,ct));
 [HttpPost("{id:guid}/preview")]public async Task<IActionResult> Preview(Guid tenant,Guid id,CartPreviewRequest request,CancellationToken ct)=>Ok(await service.Preview(tenant,id,request,false,Capability,ct));
 [HttpPost("{id:guid}/prepare")]public async Task<IActionResult> Prepare(Guid tenant,Guid id,PrepareCartRequest request,CancellationToken ct)=>Ok(await service.Prepare(tenant,id,request,false,Capability,ct));
 [HttpPost("{id:guid}/merge")]public async Task<IActionResult> Merge(Guid tenant,Guid id,MergeCartRequest request,CancellationToken ct)=>Ok(await service.Merge(tenant,id,request,Capability,ct));
}
