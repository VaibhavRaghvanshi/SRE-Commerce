using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Infrastructure;
using OpenIddict.Abstractions;

namespace SRECommerce.Identity.API;
public sealed class ActiveSessionRequirement : IAuthorizationRequirement;
