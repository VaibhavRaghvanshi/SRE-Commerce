using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;

namespace SRECommerce.Catalog.API;

[ApiController]
public sealed class MediaAssetsController(MediaAssetService assets) : ControllerBase
{
    [Authorize, HttpPost("api/v1/tenants/{tenant:guid}/catalog/media-assets"), RequestSizeLimit(800000), RequestFormLimits(MultipartBodyLengthLimit = 800000)]
    public async Task<IActionResult> Upload(Guid tenant, IFormFile file, CancellationToken ct)
    {
        if (file.Length is < 45 or > ProductImageRules.MaximumBytes) throw new BusinessFault("InvalidProductImageSize", 400);
        await using var content = new MemoryStream(); await file.CopyToAsync(content, ct);
        return StatusCode(201, await assets.Upload(tenant, content.ToArray(), ct));
    }
    [AllowAnonymous, HttpGet("api/v1/public/tenants/{tenant:guid}/catalog/media/{id:guid}")]
    public async Task<IActionResult> Read(Guid tenant, Guid id, CancellationToken ct)
    {
        var asset = await assets.Public(tenant, id, ct);
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(asset.Stream, asset.ContentType, enableRangeProcessing: false);
    }
}
