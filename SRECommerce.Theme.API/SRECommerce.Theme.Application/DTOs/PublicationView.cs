using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record PublicationView(Guid VersionId,long Sequence,string PublicationVersion,string ThemeVersion);
