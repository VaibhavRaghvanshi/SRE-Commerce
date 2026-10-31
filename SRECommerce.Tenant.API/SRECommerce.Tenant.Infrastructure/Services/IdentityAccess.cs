using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DnsClient;
using Microsoft.AspNetCore.Http;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;

public sealed record IdentityAccess(Guid UserId,string AccountKind,bool IsPlatformAdmin,Guid? SelectedTenantId,string? TenantRole,string[] Scopes,string[] Audiences);
