using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Inventory.Application;
namespace SRECommerce.Inventory.API;
[ApiController,Authorize,RequestSizeLimit(1048576),Route("api/v1/tenants/{tenant:guid}/inventory")]
public sealed class InventoryController(IInventoryService service):ControllerBase
{
    [HttpPost("opening-imports")]public async Task<IActionResult> Import(Guid tenant,OpeningStockImport request,CancellationToken ct)=>Ok(await service.ImportOpeningStock(tenant,request,ct));
    [HttpGet("lookups")]public async Task<IActionResult> Lookups(Guid tenant,CancellationToken ct)=>Ok(await service.Lookups(tenant,ct));
    [HttpPost("branches")]public async Task<IActionResult> Branch(Guid tenant,BranchRequest request,CancellationToken ct)=>StatusCode(201,await service.Branch(tenant,null,request,ct));
    [HttpPut("branches/{id:guid}")]public async Task<IActionResult> Branch(Guid tenant,Guid id,BranchRequest request,CancellationToken ct)=>Ok(await service.Branch(tenant,id,request,ct));
    [HttpPost("warehouses")]public async Task<IActionResult> Warehouse(Guid tenant,WarehouseRequest request,CancellationToken ct)=>StatusCode(201,await service.Warehouse(tenant,null,request,ct));
    [HttpPut("warehouses/{id:guid}")]public async Task<IActionResult> Warehouse(Guid tenant,Guid id,WarehouseRequest request,CancellationToken ct)=>Ok(await service.Warehouse(tenant,id,request,ct));
    [HttpPost("items")]public async Task<IActionResult> Register(Guid tenant,RegisterItemRequest request,CancellationToken ct)=>Ok(await service.Register(tenant,request,ct));
    [HttpGet("balances")]public async Task<IActionResult> Balances(Guid tenant,[FromQuery]Guid? warehouse,[FromQuery]Guid? variant,CancellationToken ct)=>Ok(await service.Balances(tenant,warehouse,variant,ct));
    [HttpGet("movements")]public async Task<IActionResult> Movements(Guid tenant,[FromQuery]Guid? balance,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Movements(tenant,balance,page,ct));
    [HttpPost("stock-changes")]public async Task<IActionResult> Change(Guid tenant,StockChangeRequest request,CancellationToken ct)=>Ok(await service.Change(tenant,request,ct));
    [HttpPost("reservations")]public async Task<IActionResult> Reserve(Guid tenant,ReserveRequest request,CancellationToken ct)=>Ok(await service.Reserve(tenant,request,false,ct));
    [HttpGet("reservations/{id:guid}")]public async Task<IActionResult> Reservation(Guid tenant,Guid id,CancellationToken ct)=>Ok(await service.Reservation(tenant,id,ct));
    [HttpPost("reservations/{id:guid}/commit")]public async Task<IActionResult> Commit(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,true,request,false,ct));
    [HttpPost("reservations/{id:guid}/release")]public async Task<IActionResult> Release(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.Close(tenant,id,false,request,false,ct));
    [HttpPost("reservations/expire")]public async Task<IActionResult> Expire(Guid tenant,CancellationToken ct)=>Ok(new{expired=await service.Expire(tenant,ct)});
    [HttpPost("suppliers")]public async Task<IActionResult> Supplier(Guid tenant,SupplierRequest request,CancellationToken ct)=>StatusCode(201,await service.Supplier(tenant,null,request,ct));
    [HttpPut("suppliers/{id:guid}")]public async Task<IActionResult> Supplier(Guid tenant,Guid id,SupplierRequest request,CancellationToken ct)=>Ok(await service.Supplier(tenant,id,request,ct));
    [HttpGet("purchases")]public async Task<IActionResult> Purchases(Guid tenant,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Purchases(tenant,page,ct));
    [HttpPost("purchases")]public async Task<IActionResult> Purchase(Guid tenant,PurchaseRequest request,CancellationToken ct)=>StatusCode(201,await service.Purchase(tenant,request,ct));
    [HttpPost("purchases/{id:guid}/submit")]public async Task<IActionResult> Submit(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.ChangePurchaseStatus(tenant,id,false,request.Version??"",ct));
    [HttpPost("purchases/{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid tenant,Guid id,CloseReservationRequest request,CancellationToken ct)=>Ok(await service.ChangePurchaseStatus(tenant,id,true,request.Version??"",ct));
    [HttpPost("purchases/{id:guid}/receive")]public async Task<IActionResult> Receive(Guid tenant,Guid id,PurchaseReceiveRequest request,CancellationToken ct)=>Ok(await service.Receive(tenant,id,request,ct));
}
