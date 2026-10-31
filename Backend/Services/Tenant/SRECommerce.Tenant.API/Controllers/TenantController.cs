using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRECommerce.Tenant.Application;

namespace SRECommerce.Tenant.API;

[ApiController,Authorize,EnableRateLimiting("tenant")]
[Route("api/v1")]
public sealed class TenantController(ITenantOperations operations) : ControllerBase
{
    [HttpGet("business-types")] public async Task<IActionResult> Types(CancellationToken ct)=>Ok(await operations.BusinessTypes(ct));
    [HttpGet("plans")] public async Task<IActionResult> Plans(CancellationToken ct)=>Ok(await operations.Plans(ct));
    [HttpPost("platform/plans")] public async Task<IActionResult> CreatePlan(PlanRequest request,CancellationToken ct){await operations.CreatePlan(request,ct);return NoContent();}
    [HttpGet("tenants")] public async Task<IActionResult> List([FromQuery]int page=1,CancellationToken ct=default)=>Ok(await operations.List(page,ct));
    [HttpPost("tenants")] public async Task<IActionResult> Register(RegistrationRequest request,[FromHeader(Name="Idempotency-Key")]string? key,CancellationToken ct)
    {
        if(!Guid.TryParse(key,out var id) || id==Guid.Empty) return BadRequest(new{error="GuidIdempotencyKeyRequired"});
        var tenant=await operations.Register(id,request,ct);return Created($"/api/v1/tenants/{tenant.Id}",tenant);
    }
    [HttpGet("tenants/{id:guid}")] public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await operations.Get(id,ct));
    [HttpPost("tenants/{id:guid}/submit")] public async Task<IActionResult> Submit(Guid id,VersionRequest request,CancellationToken ct)=>Ok(await operations.Submit(id,request,ct));
    [HttpPost("platform/tenants/{id:guid}/activate")] public async Task<IActionResult> Activate(Guid id,VersionRequest request,CancellationToken ct)=>Ok(await operations.Activate(id,request,ct));
    [HttpPost("platform/tenants/{id:guid}/suspend")] public async Task<IActionResult> Suspend(Guid id,SuspensionRequest request,CancellationToken ct)=>Ok(await operations.Suspend(id,request,ct));
    [HttpPost("platform/tenants/{id:guid}/archive")] public async Task<IActionResult> Archive(Guid id,VersionRequest request,CancellationToken ct)=>Ok(await operations.Archive(id,request,ct));
    [HttpGet("tenants/{id:guid}/settings")] public async Task<IActionResult> Settings(Guid id,CancellationToken ct)=>Ok(await operations.Settings(id,ct));
    [HttpPut("tenants/{id:guid}/settings")] public async Task<IActionResult> Settings(Guid id,SettingsRequest request,CancellationToken ct)=>Ok(await operations.UpdateSettings(id,request,ct));
    [HttpGet("tenants/{id:guid}/entitlements")] public async Task<IActionResult> Entitlements(Guid id,CancellationToken ct)=>Ok(await operations.Entitlements(id,ct));
    [HttpPut("tenants/{id:guid}/modules/{module}")] public async Task<IActionResult> Module(Guid id,string module,ModuleRequest request,CancellationToken ct)=>Ok(await operations.SetModule(id,module,request,ct));
    [HttpPut("platform/tenants/{id:guid}/subscription")] public async Task<IActionResult> Subscription(Guid id,SubscriptionRequest request,CancellationToken ct)=>Ok(await operations.SetSubscription(id,request,ct));
    [HttpGet("tenants/{id:guid}/locations")] public async Task<IActionResult> Locations(Guid id,CancellationToken ct)=>Ok(await operations.Locations(id,ct));
    [HttpPost("tenants/{id:guid}/locations")] public async Task<IActionResult> Location(Guid id,LocationRequest request,CancellationToken ct)=>Ok(await operations.AddLocation(id,request,ct));
    [HttpPost("tenants/{id:guid}/locations/{locationId:guid}/deactivate")] public async Task<IActionResult> Location(Guid id,Guid locationId,VersionRequest request,CancellationToken ct)=>Ok(await operations.DeactivateLocation(id,locationId,request,ct));
    [HttpGet("tenants/{id:guid}/domains")] public async Task<IActionResult> Domains(Guid id,CancellationToken ct)=>Ok(await operations.Domains(id,ct));
    [HttpPost("tenants/{id:guid}/domains")] public async Task<IActionResult> Domain(Guid id,DomainRequest request,CancellationToken ct)=>Ok(await operations.AddDomain(id,request,ct));
    [HttpPost("tenants/{id:guid}/domains/{domainId:guid}/verify")] public async Task<IActionResult> VerifyDomain(Guid id,Guid domainId,DomainVerificationRequest request,CancellationToken ct)=>Ok(await operations.VerifyDomain(id,domainId,request,ct));
    [HttpPost("tenants/{id:guid}/domains/{domainId:guid}/deactivate")] public async Task<IActionResult> Domain(Guid id,Guid domainId,VersionRequest request,CancellationToken ct)=>Ok(await operations.DeactivateDomain(id,domainId,request,ct));
    [HttpGet("tenants/{id:guid}/audit")] public async Task<IActionResult> Audit(Guid id,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await operations.Audit(id,page,ct));
    [HttpGet("platform/audit")] public async Task<IActionResult> PlatformHistory([FromQuery]int page=1,CancellationToken ct=default)=>Ok(await operations.PlatformHistory(page,ct));
}
