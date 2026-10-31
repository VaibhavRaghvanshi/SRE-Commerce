using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record RollbackPage(Guid LayoutVersionId,[Required]string PublicationVersion);
