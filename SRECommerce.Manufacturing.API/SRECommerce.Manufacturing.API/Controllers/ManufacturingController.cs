using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Manufacturing.Application;

namespace SRECommerce.Manufacturing.API;

[ApiController, Authorize, RequestSizeLimit(16384), Route("api/v1/tenants/{tenant:guid}/manufacturing")]
public sealed class ManufacturingController(ManufacturingService service) : ControllerBase
{
    [HttpGet("boms")] public async Task<IActionResult> Boms(Guid tenant, CancellationToken ct, [FromQuery] int page = 1) => Ok(await service.Boms(tenant, page, ct));
    [HttpPost("boms")] public async Task<IActionResult> CreateBom(Guid tenant, BomInput input, CancellationToken ct) => Ok(await service.CreateBom(tenant, input, ct));
    [HttpGet("production")] public async Task<IActionResult> Orders(Guid tenant, CancellationToken ct, [FromQuery] int page = 1) => Ok(await service.Orders(tenant, page, ct));
    [HttpPost("production")] public async Task<IActionResult> Create(Guid tenant, ProductionInput input, CancellationToken ct) => Ok(await service.Create(tenant, input, ct));
    [HttpGet("production/{id:guid}")] public async Task<IActionResult> Get(Guid tenant, Guid id, CancellationToken ct) => Ok(await service.Get(tenant, id, ct));
    [HttpGet("production/{id:guid}/history")] public async Task<IActionResult> History(Guid tenant, Guid id, CancellationToken ct) => Ok(await service.History(tenant, id, ct));
    [HttpPost("production/{id:guid}/start")] public async Task<IActionResult> Start(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) => Accepted(await service.Start(tenant, id, input, ct));
    [HttpPost("production/{id:guid}/stage")] public async Task<IActionResult> Stage(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) => Ok(await service.Stage(tenant, id, input, ct));
    [HttpPost("production/{id:guid}/quality")] public async Task<IActionResult> Quality(Guid tenant, Guid id, QualityInput input, CancellationToken ct) => Ok(await service.Quality(tenant, id, input, ct));
    [HttpPost("production/{id:guid}/finish")] public async Task<IActionResult> Finish(Guid tenant, Guid id, ProductionFinish input, CancellationToken ct) => Accepted(await service.Finish(tenant, id, input, ct));
    [HttpPost("production/{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) => Accepted(await service.Cancel(tenant, id, input, ct));
    [HttpPost("production/{id:guid}/retry")] public async Task<IActionResult> Retry(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) => Accepted(await service.Retry(tenant, id, input, ct));
}
