using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record VersionView(Guid Id,Guid PageId,int Revision,string Hash,DateTimeOffset CreatedAt);
