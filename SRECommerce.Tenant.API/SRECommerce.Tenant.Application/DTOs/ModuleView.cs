using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record ModuleView(string Module,bool Configured,bool Entitled,bool Enabled);
