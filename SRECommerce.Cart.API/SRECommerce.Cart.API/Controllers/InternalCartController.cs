using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Cart.Application;
namespace SRECommerce.Cart.API;
[ApiController,Authorize,Route("internal/v1/tenants/{tenant:guid}/cart")]
public sealed class InternalCartController(ICartService service):ControllerBase
{
 [HttpPost("claims")]public async Task<IActionResult> Claim(Guid tenant,CartClaimRequest request,CancellationToken ct)=>Ok(await service.Claim(tenant,request,Request.Headers["X-Cart-Key"].ToString(),ct));
 [HttpPost("claims/cancel")]public async Task<IActionResult> Cancel(Guid tenant,CartClaimRequest request,CancellationToken ct)=>Ok(await service.CancelClaim(tenant,request,ct));
 [HttpGet("claims/{order:guid}")]public async Task<IActionResult> Claim(Guid tenant,Guid order,CancellationToken ct)=>Ok(await service.OrderClaim(tenant,order,ct));
 [HttpPost("claims/{order:guid}/complete")]public async Task<IActionResult> Complete(Guid tenant,Guid order,CartClaimVersion request,CancellationToken ct)=>Ok(await service.CloseClaim(tenant,order,true,request.Version,ct));
 [HttpPost("claims/{order:guid}/release")]public async Task<IActionResult> Release(Guid tenant,Guid order,CartClaimVersion request,CancellationToken ct)=>Ok(await service.CloseClaim(tenant,order,false,request.Version,ct));
 [HttpPost("preparations/{id:guid}/verify")]public async Task<IActionResult> Verify(Guid tenant,Guid id,PreparationProof proof,CancellationToken ct)=>Ok(await service.ForOrder(tenant,id,proof,Request.Headers["X-Cart-Key"].ToString(),ct));
 [HttpPost("preparations/{id:guid}/shipping-verify")]public async Task<IActionResult> Shipping(Guid tenant,Guid id,PreparationProof proof,CancellationToken ct)=>Ok(await service.ForShipping(tenant,id,proof,Request.Headers["X-Cart-Key"].ToString(),ct));
}
