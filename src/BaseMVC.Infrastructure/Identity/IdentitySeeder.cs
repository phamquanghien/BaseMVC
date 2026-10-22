using System.Security.Claims;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Identity;

public sealed class IdentitySeeder(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var roleName in IdentitySeedRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed Identity role '{roleName}': {FormatErrors(result)}");
            }
        }

        await SeedRolePermissionsAsync();

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No initial administrator was created. Configure SeedAdmin:Email and SeedAdmin:Password using a secret provider.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Both SeedAdmin:Email and SeedAdmin:Password must be configured to seed the initial administrator.");
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the initial administrator: {FormatErrors(createResult)}");
            }
        }

        if (await userManager.IsInRoleAsync(user, IdentitySeedRoles.SuperAdmin))
        {
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, IdentitySeedRoles.SuperAdmin);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the initial administrator role: {FormatErrors(roleResult)}");
        }

        logger.LogInformation(
            "The configured initial administrator has been assigned the {Role} role.",
            IdentitySeedRoles.SuperAdmin);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    private async Task SeedRolePermissionsAsync()
    {
        foreach (var roleName in IdentitySeedRoles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException(
                    $"Identity role '{roleName}' was not found after seeding.");
            var existingPermissions = (await roleManager.GetClaimsAsync(role))
                .Where(claim => claim.Type == PermissionClaim.Type)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in PermissionCatalog.ForRole(roleName))
            {
                if (existingPermissions.Contains(permission.Name))
                {
                    continue;
                }

                var result = await roleManager.AddClaimAsync(
                    role,
                    new Claim(PermissionClaim.Type, permission.Name));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not seed permission '{permission.Name}' for role '{roleName}': {FormatErrors(result)}");
                }
            }
        }
    }
}
