using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record VersionView(Guid Id,Guid ThemeId,int Revision,string Hash,DateTimeOffset CreatedAt);
