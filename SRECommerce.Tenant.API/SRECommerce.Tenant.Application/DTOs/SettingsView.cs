using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record SettingsView(string Currency,string Culture,string TimeZone,string DisplayName,string? LogoUrl);
