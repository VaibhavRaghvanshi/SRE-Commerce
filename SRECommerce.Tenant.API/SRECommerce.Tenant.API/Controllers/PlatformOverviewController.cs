using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRECommerce.Tenant.Application;

namespace SRECommerce.Tenant.API;

[ApiController, Authorize, Route("api/v1/platform"), EnableRateLimiting("tenant")]
public sealed class PlatformOverviewController(IPlatformOverview overview) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await overview.Summary(ct));
    [HttpGet("service-health"), EnableRateLimiting("platform-health")]
    public async Task<IActionResult> Health(CancellationToken ct) => Ok(await overview.Health(ct));
}
