using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record PublishTheme([Required]string Version,string? PublicationVersion);
