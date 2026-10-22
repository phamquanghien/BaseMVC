using System.Security.Claims;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Tests.Authorization;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task Dynamic_policy_reads_role_permission_grants_and_revocations_from_database()
    {
        const string permission = "Permissions.Reports.Export";
        const string userId = "user-1";
        const string roleId = "role-1";

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "report-user",
            NormalizedUserName = "REPORT-USER"
        });
        dbContext.Roles.Add(new IdentityRole
        {
            Id = roleId,
            Name = "ReportManager",
            NormalizedName = "REPORTMANAGER"
        });
        dbContext.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = roleId
        });
        await dbContext.SaveChangesAsync();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                authenticationType: "Test"));
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var anonymousDenied = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            permission);
        Assert.False(anonymousDenied.Succeeded);

        var malformedPolicyDenied = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            "Permissions.Reports.Export.Extra");
        Assert.False(malformedPolicyDenied.Succeeded);

        var deniedBeforeGrant = await authorization.AuthorizeAsync(principal, resource: null, permission);
        Assert.False(deniedBeforeGrant.Succeeded);

        var roleClaim = new IdentityRoleClaim<string>
        {
            RoleId = roleId,
            ClaimType = PermissionClaim.Type,
            ClaimValue = permission
        };
        dbContext.RoleClaims.Add(roleClaim);
        await dbContext.SaveChangesAsync();

        var granted = await authorization.AuthorizeAsync(principal, resource: null, permission);
        Assert.True(granted.Succeeded);

        dbContext.RoleClaims.Remove(roleClaim);
        await dbContext.SaveChangesAsync();

        var deniedAfterRevocation = await authorization.AuthorizeAsync(principal, resource: null, permission);
        Assert.False(deniedAfterRevocation.Succeeded);
    }

    [Theory]
    [InlineData("Permissions.Users.Read", true)]
    [InlineData("Permissions.Reports.Export", true)]
    [InlineData("Permissions.Users.Read.All", false)]
    [InlineData("Permissions..Read", false)]
    [InlineData("Permissions.Users.Read-Write", false)]
    [InlineData("Roles.Read", false)]
    public void Permission_names_require_two_ascii_identifiers(
        string permission,
        bool expectedValidity)
    {
        Assert.Equal(expectedValidity, PermissionClaim.IsValid(permission));
    }

    [Fact]
    public void Permission_catalog_contains_unique_valid_policy_names()
    {
        var names = PermissionCatalog.All.Select(permission => permission.Name).ToArray();

        Assert.All(names, permission => Assert.True(PermissionClaim.IsValid(permission)));
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
