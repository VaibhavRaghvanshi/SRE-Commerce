using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record PublicLayout(Guid TenantId,string Slug,long Sequence,string Source,string BusinessType,LayoutDocument Document);
