using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Inventory.Application;
namespace SRECommerce.Inventory.API;
[ApiController,Authorize(Policy="Machine"),Route("internal/v1/tenants/{tenant:guid}/inventory")]
public sealed class InternalInventoryController(IInventoryService service):ControllerBase
{
    [HttpPost("reservations")]public async Task<IActionResult> Reserve(Guid tenant,ReserveRequest request,CancellationToken ct)=>Ok(await service.Reserve(tenant,request,true,ct));
    [HttpPost("reservations/{id:guid}/commit")]public async Task<IActionResult> Commit(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,true,request,true,ct));
    [HttpPost("reservations/{id:guid}/release")]public async Task<IActionResult> Release(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,false,request,true,ct));
}
