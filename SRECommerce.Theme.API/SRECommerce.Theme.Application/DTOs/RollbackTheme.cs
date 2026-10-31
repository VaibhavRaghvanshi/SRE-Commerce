using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record RollbackTheme(Guid ThemeVersionId,[Required]string PublicationVersion);
