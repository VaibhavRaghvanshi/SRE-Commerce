using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.API;

[ApiController, Authorize, RequestSizeLimit(8192), Route("api/v1/tenants/{tenant:guid}/notifications")]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet("templates")] public async Task<IActionResult> Templates(Guid tenant, CancellationToken ct) => Ok(await service.Templates(tenant, ct));
    [HttpPut("templates/{kind}/{channel}")] public async Task<IActionResult> SetTemplate(Guid tenant, NotificationKind kind, NotificationChannel channel, TemplateInput input, CancellationToken ct) => Ok(await service.SetTemplate(tenant, kind, channel, input, ct));
    [HttpGet("preferences")] public async Task<IActionResult> Preferences(Guid tenant, CancellationToken ct) => Ok(await service.Preferences(tenant, ct));
    [HttpGet("stock-alerts")] public async Task<IActionResult> StockSubscription(Guid tenant, CancellationToken ct) => Ok(await service.StockSubscription(tenant, ct));
    [HttpPut("stock-alerts")] public async Task<IActionResult> StockSubscription(Guid tenant, StockSubscriptionInput input, CancellationToken ct) => Ok(await service.SetStockSubscription(tenant, input, ct));
    [HttpPut("preferences")] public async Task<IActionResult> SetPreferences(Guid tenant, PreferenceInput input, CancellationToken ct) => Ok(await service.SetPreferences(tenant, input, ct));
    [HttpGet("jobs")] public async Task<IActionResult> Jobs(Guid tenant, CancellationToken ct, [FromQuery] int page = 1) => Ok(await service.Jobs(tenant, page, ct));
    [HttpGet("jobs/{id:guid}/{channel}/attempts")] public async Task<IActionResult> Attempts(Guid tenant, Guid id, NotificationChannel channel, CancellationToken ct) => Ok(await service.Attempts(tenant, id, channel, ct));
}

[ApiController, Authorize(Policy = "Machine"), RequestSizeLimit(4096), Route("internal/v1/tenants/{tenant:guid}/notifications")]
public sealed class InternalNotificationsController(NotificationService service) : ControllerBase
{
    [HttpPost("order")] public async Task<IActionResult> Queue(Guid tenant, OrderNoticeRequest input, CancellationToken ct) => Ok(await service.QueueOrder(tenant, input, ct));
}
