using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record LocationView(Guid Id,string Name,string Address,string CountryCode,bool IsActive);
