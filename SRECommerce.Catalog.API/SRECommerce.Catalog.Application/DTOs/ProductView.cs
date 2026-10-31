using System.Text.Json;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Application;
public record ProductView(Guid Id,string Name,string Slug,Guid CategoryId,Guid? BrandId,ProductKind Kind,ProductStatus Status,
    string Description,string SeoTitle,string SeoDescription,bool Visible,bool B2BOnly,Dictionary<string,JsonElement> Attributes,
    Dictionary<string,string> Specifications,VariantInput[] Variants,MediaInput[] Media,BundleInput[] BundleItems,RelationshipInput[] Relationships,
    int PublishedRevision,string? Version);
