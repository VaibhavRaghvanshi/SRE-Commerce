using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Domain.Common;
using SRECommerce.Api.Security;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/admin/uploads")]
[Authorize(Roles = Roles.Staff)]
public class AdminUploadsController(IWebHostEnvironment env) : ControllerBase
{
    public class UploadImageRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AdminUploadLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AdminUploadLimits.MaxRequestBytes)]
    public async Task<ActionResult<object>> Upload([FromForm] UploadImageRequest req)
    {
        var file = req.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        if (file.Length > AdminUploadLimits.MaxImageBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Images must be 5 MiB or smaller." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var formats = new Dictionary<string, string>
        {
            [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif", [".webp"] = "image/webp"
        };
        if (!formats.TryGetValue(extension, out var contentType)
            || !contentType.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PNG, JPEG, GIF and WebP images with matching content types are allowed." });

        await using var source = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await source.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, HttpContext.RequestAborted);
        if (bytesRead < header.Length || !HasImageSignature(header, extension))
            return BadRequest(new { error = "The image signature does not match its extension." });

        var wwwroot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(wwwroot))
        {
            wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var uploadsPath = Path.Combine(wwwroot, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid():N}{(extension == ".jpeg" ? ".jpg" : extension)}";
        var filePath = Path.Combine(uploadsPath, fileName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(header, HttpContext.RequestAborted);
            await source.CopyToAsync(stream, HttpContext.RequestAborted);
        }
        catch
        {
            // Remove only this request's newly created partial file.
            System.IO.File.Delete(filePath);
            throw;
        }

        var url = $"/uploads/{fileName}";

        return Ok(new { url });
    }

    private static bool HasImageSignature(ReadOnlySpan<byte> header, string extension) => extension switch
    {
        ".png" => header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
        ".gif" => header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8),
        ".webp" => header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };
}

