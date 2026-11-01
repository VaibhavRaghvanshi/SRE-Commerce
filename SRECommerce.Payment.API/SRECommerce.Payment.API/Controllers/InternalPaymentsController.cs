using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.API;
[ApiController, Authorize(Policy = "Machine"), RequestSizeLimit(16384), Route("internal/v1/tenants/{tenant:guid}/payments")]
public sealed class InternalPaymentsController(IPaymentService service) : ControllerBase
{
    [HttpPost] public async Task<IActionResult> Start(Guid tenant, PaymentStartRequest request, CancellationToken ct) => Ok(await service.Start(tenant, request.OrderId, ct));
    [HttpGet("orders/{order:guid}")] public async Task<IActionResult> Get(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.Read(tenant, order, true, false, ct));
    [HttpPost("orders/{order:guid}/capture")] public async Task<IActionResult> Capture(Guid tenant, Guid order, PaymentVersionRequest request, CancellationToken ct) => Ok(await service.Capture(tenant, order, request.Version, ct));
    [HttpPost("orders/{order:guid}/abort")] public async Task<IActionResult> Abort(Guid tenant, Guid order, CancellationToken ct) => Ok(await service.Abort(tenant, order, ct));
    [HttpPost("orders/{order:guid}/refunds")] public async Task<IActionResult> Refund(Guid tenant, Guid order, RefundRequest request, CancellationToken ct) => Ok(await service.Refund(tenant, order, request, ct));
}
