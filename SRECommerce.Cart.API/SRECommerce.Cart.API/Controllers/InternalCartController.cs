using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using SRECommerce.Cart.Application;
namespace SRECommerce.Cart.API;
[ApiController,Authorize,Route("internal/v1/tenants/{tenant:guid}/cart")]
public sealed class InternalCartController(ICartService service):ControllerBase
{
 [HttpPost("preparations/{id:guid}/verify")]public async Task<IActionResult> Verify(Guid tenant,Guid id,PreparationProof proof,CancellationToken ct)=>Ok(await service.ForOrder(tenant,id,proof,Request.Headers["X-Cart-Key"].ToString(),ct));
}
