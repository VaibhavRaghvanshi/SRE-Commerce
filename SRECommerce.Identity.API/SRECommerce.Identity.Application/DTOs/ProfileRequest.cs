using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record ProfileRequest([Required, StringLength(160, MinimumLength=1)] string FullName);
