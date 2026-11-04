using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Order.Application;

namespace SRECommerce.Order.API;

[ApiController, Authorize, Route("api/v1/tenants/{tenant:guid}/orders/reports")]
public sealed class SalesReportsController(SalesReportService reports) : ControllerBase
{
    [HttpGet("sales")]
    public async Task<IActionResult> Sales(Guid tenant, [FromQuery] SalesReportQuery query, CancellationToken ct) => Ok(await reports.Read(tenant, query, ct));
}
