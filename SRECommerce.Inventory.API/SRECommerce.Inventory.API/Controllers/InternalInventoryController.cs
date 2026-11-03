using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Inventory.Application;
namespace SRECommerce.Inventory.API;
[ApiController,Authorize(Policy="Machine"),Route("internal/v1/tenants/{tenant:guid}/inventory")]
public sealed class InternalInventoryController(IInventoryService service):ControllerBase
{
    [HttpGet("warehouses/{id:guid}")]public async Task<IActionResult> Warehouse(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.WarehouseForService(tenant,id,ct));
    [HttpGet("warehouses")]public async Task<IActionResult> Warehouses(Guid tenant,[FromQuery]Guid[] ids,CancellationToken ct)=>Ok(await service.WarehousesForService(tenant,ids,ct));
    [HttpPost("reservations/{id:guid}/cancel")]public async Task<IActionResult> Abort(Guid tenant,Guid id,ReservationAbortRequest request,CancellationToken ct)=>Ok(await service.Abort(tenant,id,request,ct));
    [HttpPost("customer-returns")]public async Task<IActionResult> Return(Guid tenant,CustomerReturnReceipt request,CancellationToken ct)=>Ok(await service.CustomerReturn(tenant,request,ct));
    [HttpPost("manufacturing-receipts")]public async Task<IActionResult> Production(Guid tenant,ManufacturingReceipt request,CancellationToken ct)=>Ok(await service.ReceiveProduction(tenant,request,ct));
    [HttpGet("allocation-options")]public async Task<IActionResult> Options(Guid tenant,[FromQuery]Guid[] variants,[FromQuery]DateTimeOffset expiresAt,CancellationToken ct)=>Ok(await service.AllocationOptions(tenant,variants,expiresAt,ct));
    [HttpGet("reservations/{id:guid}")]public async Task<IActionResult> Reservation(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.RecoveryReservation(tenant,id,ct));
    [HttpPost("reservations")]public async Task<IActionResult> Reserve(Guid tenant,ReserveRequest request,CancellationToken ct)=>Ok(await service.Reserve(tenant,request,true,ct));
    [HttpPost("reservations/{id:guid}/commit")]public async Task<IActionResult> Commit(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,true,request,true,ct));
    [HttpPost("reservations/{id:guid}/release")]public async Task<IActionResult> Release(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,false,request,true,ct));
}
