using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRECommerce.Catalog.Application;
namespace SRECommerce.Catalog.API;
[ApiController,Authorize,RequestSizeLimit(1048576),Route("api/v1/tenants/{tenant:guid}/catalog")]
public sealed class CatalogController(ICatalogService catalog):ControllerBase
{
    [HttpGet("lookups")]public async Task<IActionResult> Lookups(Guid tenant,CancellationToken ct)=>Ok(await catalog.Lookups(tenant,false,ct));
    [HttpPost("categories")]public async Task<IActionResult> Category(Guid tenant,CategoryRequest request,CancellationToken ct){var value=await catalog.Category(tenant,null,request,ct);return StatusCode(201,value);}
    [HttpPut("categories/{id:guid}")]public async Task<IActionResult> Category(Guid tenant,Guid id,CategoryRequest request,CancellationToken ct)=>Ok(await catalog.Category(tenant,id,request,ct));
    [HttpPut("categories/{id:guid}/attributes")]public async Task<IActionResult> Bind(Guid tenant,Guid id,[FromQuery]string version,[FromBody]AttributeBinding[] request,CancellationToken ct){await catalog.Bind(tenant,id,version,request,ct);return NoContent();}
    [HttpGet("categories/{id:guid}/attributes")]public async Task<IActionResult> Attributes(Guid tenant,Guid id,CancellationToken ct)=>Ok(await catalog.AttributesFor(tenant,id,ct));
    [HttpPost("brands")]public async Task<IActionResult> Brand(Guid tenant,BrandRequest request,CancellationToken ct)=>StatusCode(201,await catalog.Brand(tenant,null,request,ct));
    [HttpPut("brands/{id:guid}")]public async Task<IActionResult> Brand(Guid tenant,Guid id,BrandRequest request,CancellationToken ct)=>Ok(await catalog.Brand(tenant,id,request,ct));
    [HttpPost("attributes")]public async Task<IActionResult> Attribute(Guid tenant,AttributeRequest request,CancellationToken ct)=>StatusCode(201,await catalog.Attribute(tenant,null,request,ct));
    [HttpPut("attributes/{id:guid}")]public async Task<IActionResult> Attribute(Guid tenant,Guid id,AttributeRequest request,CancellationToken ct)=>Ok(await catalog.Attribute(tenant,id,request,ct));
    [HttpGet("products")]public async Task<IActionResult> Products(Guid tenant,[FromQuery]CatalogQuery query,CancellationToken ct)=>Ok(await catalog.Products(tenant,query,false,ct));
    [HttpGet("products/{id:guid}")]public async Task<IActionResult> Product(Guid tenant,Guid id,CancellationToken ct)=>Ok(await catalog.Product(tenant,id,ct));
    [HttpGet("variants/{id:guid}")]public async Task<IActionResult> Variant(Guid tenant,Guid id,CancellationToken ct)=>Ok(await catalog.PublishedVariantForStaff(tenant,id,ct));
    [HttpGet("shopping-variants")]public async Task<IActionResult> ShoppingVariants(Guid tenant,[FromQuery]Guid[] ids,CancellationToken ct)=>Ok(await catalog.ShoppingVariantIds(tenant,ids,ct));
    [HttpGet("published-skus")]public async Task<IActionResult> PublishedSkus(Guid tenant,[FromQuery]Guid[] ids,CancellationToken ct)=>Ok(await catalog.PublishedSkus(tenant,ids,ct));
    [HttpPost("publish-batch")]public async Task<IActionResult> PublishBatch(Guid tenant,PublishBatchRequest request,CancellationToken ct)=>Ok(await catalog.PublishBatch(tenant,request,ct));
    [HttpPost("products")]public async Task<IActionResult> Create(Guid tenant,ProductDraft request,CancellationToken ct)=>StatusCode(201,await catalog.Save(tenant,null,request,ct));
    [HttpPut("products/{id:guid}")]public async Task<IActionResult> Save(Guid tenant,Guid id,ProductDraft request,CancellationToken ct)=>Ok(await catalog.Save(tenant,id,request,ct));
    [HttpPost("products/{id:guid}/publish")]public async Task<IActionResult> Publish(Guid tenant,Guid id,VersionRequest request,CancellationToken ct)=>Ok(await catalog.Publish(tenant,id,request.Version,ct));
    [HttpPost("products/{id:guid}/archive")]public async Task<IActionResult> Archive(Guid tenant,Guid id,VersionRequest request,CancellationToken ct){await catalog.Archive(tenant,id,request.Version,ct);return NoContent();}
    [HttpGet("export")]public async Task<IActionResult> Export(Guid tenant,[FromQuery]CatalogQuery query,CancellationToken ct)=>Ok(await catalog.Products(tenant,query,false,ct));
    [HttpPost("import")]public async Task<IActionResult> Import(Guid tenant,ImportRequest request,CancellationToken ct)=>Ok(await catalog.Import(tenant,request,ct));
    [HttpPost("collections")]public async Task<IActionResult> Collection(Guid tenant,CollectionRequest request,CancellationToken ct)=>StatusCode(201,await catalog.Collection(tenant,null,request,ct));
    [HttpPut("collections/{id:guid}")]public async Task<IActionResult> Collection(Guid tenant,Guid id,CollectionRequest request,CancellationToken ct)=>Ok(await catalog.Collection(tenant,id,request,ct));
}
