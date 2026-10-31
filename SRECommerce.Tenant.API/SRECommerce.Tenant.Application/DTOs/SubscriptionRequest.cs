using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record SubscriptionRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(40)]string PlanCode,DateTimeOffset StartsAt,DateTimeOffset EndsAt,SubscriptionStatus Status);
