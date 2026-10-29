using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace SRECommerce.Identity.API;

public static class IdentitySetup
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = config["Client:Id"] ?? (environment.IsDevelopment() ? "srecommerce-local" : throw new InvalidOperationException("Client:Id required."));
        var redirect = new Uri(config["Client:RedirectUri"] ?? (environment.IsDevelopment() ? "http://127.0.0.1:5099/callback" : throw new InvalidOperationException("Client:RedirectUri required.")));
        if ((!environment.IsDevelopment() && redirect.Scheme != "https") || !string.IsNullOrEmpty(redirect.Fragment)) throw new InvalidOperationException("Invalid client redirect URI.");
        if (await manager.FindByClientIdAsync(clientId) is null)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = clientId, ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Implicit,
                DisplayName = "SRECommerce first-party development client", RedirectUris = { redirect },
                Permissions = { Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation, Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email, Permissions.Scopes.Profile, Permissions.Prefixes.Scope + "identity_api", Permissions.Prefixes.Scope + "tenant_api" },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            });
        // Explicit initialization upgrades this client's allowed API scope while
        // preserving its existing redirects, requirements and other settings.
        var application = (await manager.FindByClientIdAsync(clientId))!;
        if (!(await manager.GetPermissionsAsync(application)).Contains(Permissions.Prefixes.Scope + "tenant_api"))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application);
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + "tenant_api");
            await manager.UpdateAsync(application, descriptor);
        }
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync("SuperAdmin")) await roles.CreateAsync(new IdentityRole<Guid>("SuperAdmin"));
        var email = config["Bootstrap:Email"];
        var password = config["Bootstrap:Password"];
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Local bootstrap is Development-only; production provisioning needs a separate reviewed procedure.");
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Bootstrap password must be supplied through local secrets.");
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var db = services.GetRequiredService<IdentityDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded) throw new InvalidOperationException("Bootstrap account rejected: " + string.Join(',', created.Errors.Select(e => e.Code)));
                await users.AddToRoleAsync(user, "SuperAdmin");
                db.Profiles.Add(new UserProfile { UserId = user.Id, FullName = "Development Platform Administrator", Kind = AccountKind.SuperAdmin });
                db.AuditLogs.Add(new SecurityAuditLog { UserId = user.Id, Action = "platform.bootstrap", Outcome = "explicit-development-provisioning" });
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
        Console.WriteLine("Identity client/role initialization completed. No source database was modified.");
    }

    public record LegacyUser(int Id, string FullName, string Email, string PasswordHash, string Role);
    public record Snapshot(string Source, LegacyUser[] Users);
    public static async Task ImportAsync(IServiceProvider services, string path, bool apply, bool linkReviewedLegacyAliases=false)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Invalid snapshot.");
        if (snapshot.Source is not ("legacy-sqlite" or "legacy-sqlserver")) throw new InvalidOperationException("Unrecognized legacy source label.");
        if (snapshot.Users.Select(u => u.Id).Distinct().Count() != snapshot.Users.Length
            || snapshot.Users.Select(u => u.Email.Trim().ToUpperInvariant()).Distinct().Count() != snapshot.Users.Length)
            throw new InvalidOperationException("Duplicate legacy IDs/normalized emails need reconciliation.");
        var db = services.GetRequiredService<IdentityDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var row in snapshot.Users)
        {
            if (!row.PasswordHash.StartsWith("$2", StringComparison.Ordinal) || row.PasswordHash.Length != 60) throw new InvalidOperationException("Unsupported legacy password representation.");
            if (!await db.LegacyUsers.AnyAsync(m => m.Source == snapshot.Source && m.LegacyId == row.Id)
                && await users.FindByEmailAsync(row.Email.Trim()) is { } existing)
            {
                if (!linkReviewedLegacyAliases || snapshot.Source != "legacy-sqlserver"
                    || !await db.LegacyUsers.AnyAsync(m=>m.Source=="legacy-sqlite" && m.UserId==existing.Id))
                    throw new InvalidOperationException("An existing target email requires an explicit mapping decision; import refused.");
            }
        }
        Console.WriteLine($"Legacy import plan: source={snapshot.Source}; users={snapshot.Users.Length}; email verification pending; no tenant or platform-admin grants; apply={apply}.");
        if (!apply) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var imported = 0;
        var aliases = 0;
        foreach (var row in snapshot.Users)
        {
            if (await db.LegacyUsers.AnyAsync(m => m.Source == snapshot.Source && m.LegacyId == row.Id)) continue;
            if (linkReviewedLegacyAliases && snapshot.Source == "legacy-sqlserver" && await users.FindByEmailAsync(row.Email.Trim()) is { } existing)
            {
                db.LegacyUsers.Add(new LegacyUserMapping{Source=snapshot.Source,LegacyId=row.Id,UserId=existing.Id,LegacyRole=row.Role});
                db.AuditLogs.Add(new SecurityAuditLog{UserId=existing.Id,Action="legacy.alias",Outcome="reviewed-provenance-only-no-password-overwrite"});
                await db.SaveChangesAsync();
                aliases++;
                continue;
            }
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = row.Email.Trim(), Email = row.Email.Trim(), EmailConfirmed = false, LockoutEnabled = true, PasswordHash = "LEGACY_BCRYPT", LegacyPasswordHash = row.PasswordHash };
            var result = await users.CreateAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException("Legacy account rejected: " + string.Join(',', result.Errors.Select(e => e.Code)));
            var profile = new UserProfile { UserId = user.Id, Kind = row.Role == "Customer" ? AccountKind.Customer : AccountKind.Staff };
            profile.Rename(row.FullName);
            db.Profiles.Add(profile);
            db.LegacyUsers.Add(new LegacyUserMapping { Source = snapshot.Source, LegacyId = row.Id, UserId = user.Id, LegacyRole = row.Role });
            db.AuditLogs.Add(new SecurityAuditLog { UserId = user.Id, Action = "legacy.import", Outcome = "unconfirmed-no-tenant-no-platform-role" });
            await db.SaveChangesAsync();
            imported++;
        }
        await transaction.CommitAsync();
        Console.WriteLine($"Legacy import completed: {imported} new accounts, {aliases} reviewed provenance aliases; existing credentials unchanged.");
    }
}
