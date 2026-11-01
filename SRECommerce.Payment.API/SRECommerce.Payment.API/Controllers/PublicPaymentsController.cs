using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.API;
[ApiController, AllowAnonymous, RequestSizeLimit(16384), Route("api/v1/public/tenants/{tenant:guid}/payments")]
public sealed class PublicPaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet("methods")] public async Task<IActionResult> Methods(Guid tenant, CancellationToken ct) => Ok(await service.Methods(tenant, ct));
    [HttpPost("webhooks/sandbox")] public async Task<IActionResult> Webhook(Guid tenant, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var document = await reader.ReadToEndAsync(ct);
        return Ok(await service.Webhook(tenant, document, Request.Headers["X-Sandbox-Timestamp"].ToString(), Request.Headers["X-Sandbox-Signature"].ToString(), ct));
    }
}
