using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record PublicationView(Guid VersionId,long Sequence,string PublicationVersion,string PageVersion);
