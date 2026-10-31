using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Application;
public record ProductDraft([Required]string Name,[Required]string Slug,Guid CategoryId,Guid? BrandId,ProductKind Kind,
    string Description,string SeoTitle,string SeoDescription,bool Visible,bool B2BOnly,
    Dictionary<string,JsonElement> Attributes,Dictionary<string,string> Specifications,
    VariantInput[] Variants,MediaInput[] Media,BundleInput[] BundleItems,RelationshipInput[] Relationships,string? Version=null);
