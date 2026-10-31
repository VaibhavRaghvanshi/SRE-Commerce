using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SRECommerce.Api.Tests;

public sealed class UploadTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aDakAAAAASUVORK5CYII=");

    private static async Task Login(HttpClient client, ApiFactory factory, string role = "employee")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{role}@example.test", password = factory.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("token").GetString());
    }

    private static MultipartFormDataContent Form(byte[] bytes, string filename, string mime)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(content, "file", filename);
        return form;
    }

    [Fact]
    public async Task Valid_image_roundtrips_as_image_with_nosniff()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory);
        using var form = Form(Png, "../../picture.png", "image/png");
        var response = await client.PostAsync("/api/admin/uploads", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        Assert.Matches("^/uploads/[a-f0-9]{32}\\.png$", url);
        var image = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("attack.html", "text/html", false)]
    [InlineData("attack.svg", "image/svg+xml", false)]
    [InlineData("fake.png", "image/png", false)]
    [InlineData("image.png", "text/html", true)]
    [InlineData("image.jpg", "image/jpeg", true)]
    public async Task Invalid_extension_mime_or_signature_is_rejected(string filename, string mime, bool usePng)
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory);
        using var form = Form(usePng ? Png : Encoding.UTF8.GetBytes("<script>alert('bad')</script>"), filename, mime);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/uploads", form)).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(factory.TestDirectory, "wwwroot", "uploads")));
    }

    [Fact]
    public async Task Oversize_image_is_rejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        await Login(client, factory);
        using var form = Form(new byte[5 * 1024 * 1024 + 1], "large.png", "image/png");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/admin/uploads", form)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_and_customer_uploads_are_denied(bool customer)
    {
        using var factory = new ApiFactory();
        using var client = factory.Start();
        if (customer) await Login(client, factory, "customer");
        using var form = Form(Png, "valid.png", "image/png");
        Assert.Equal(customer ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, (await client.PostAsync("/api/admin/uploads", form)).StatusCode);
    }
}
