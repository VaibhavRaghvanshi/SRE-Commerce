using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Payment.Application;
using SRECommerce.Payment.Domain;
namespace SRECommerce.Payment.API;
[ApiController, Authorize, RequestSizeLimit(16384), Route("api/v1/tenants/{tenant:guid}/payments")]
public sealed class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(Guid tenant, [FromQuery] int page = 1, [FromQuery] PaymentStatus? status = null, CancellationToken ct = default) => Ok(await service.List(tenant, page, status, ct));
    [HttpGet("configuration")] public async Task<IActionResult> Config(Guid tenant, CancellationToken ct) => Ok(await service.Configuration(tenant, null, ct));
    [HttpPut("configuration")] public async Task<IActionResult> Config(Guid tenant, PaymentConfigRequest request, CancellationToken ct) => Ok(await service.Configuration(tenant, request, ct));
    [HttpGet("orders/{order:guid}")] public async Task<IActionResult> Get(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.Read(tenant, order, false, false, ct));
    [HttpGet("mine/orders/{order:guid}")] public async Task<IActionResult> Mine(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.Read(tenant, order, false, true, ct));
    [HttpGet("orders/{order:guid}/history")] public async Task<IActionResult> History(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.History(tenant, order, ct));
    [HttpPost("orders/{order:guid}/reconcile")] public async Task<IActionResult> Reconcile(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.Reconcile(tenant, order, ct));
    [HttpPost("orders/{order:guid}/sandbox-decision")] public async Task<IActionResult> Decide(Guid tenant, Guid order, SandboxDecisionRequest request, CancellationToken ct) => Ok(await service.Decide(tenant, order, request, ct));
}
