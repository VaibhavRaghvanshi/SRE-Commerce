using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;
using SRECommerce.Api.Security;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = SecurityPolicies.ManageUsers)]
public class AdminUsersController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Admin,
        Roles.Employee,
        Roles.Customer
    };

    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> List()
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Role == Roles.Admin ? 0 : u.Role == Roles.Employee ? 1 : 2)
            .ThenByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto(u.Id, u.FullName, u.Email, u.Role, u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create(CreateStaffUserRequest request)
    {
        var role = NormalizeRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Role must be Admin, Employee, or Customer." });
        }

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Full name, email, and password are required." });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new AdminUserDto(user.Id, user.FullName, user.Email, user.Role, user.CreatedAt));
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] string role)
    {
        var normalized = NormalizeRole(role);
        if (normalized is null)
        {
            return BadRequest(new { message = "Role must be Admin, Employee, or Customer." });
        }

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Role == Roles.Admin && normalized != Roles.Admin
            && await db.Users.CountAsync(u => u.Role == Roles.Admin) <= 1)
            return BadRequest(new { message = "At least one admin account must remain." });

        user.Role = normalized;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Role == Roles.Admin && await db.Users.CountAsync(u => u.Role == Roles.Admin) <= 1)
        {
            return BadRequest(new { message = "At least one admin account must remain." });
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;

        var match = AllowedRoles.FirstOrDefault(r => r.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
        return match;
    }
}

