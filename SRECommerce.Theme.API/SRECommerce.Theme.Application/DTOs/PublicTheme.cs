using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record PublicTheme(Guid TenantId,long Sequence,string Source,string BusinessType,ThemeDocument Document,IReadOnlyDictionary<string,string> Variables);
