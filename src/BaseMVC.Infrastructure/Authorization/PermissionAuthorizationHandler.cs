using System.Security.Claims;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(
    ApplicationDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!PermissionClaim.IsValid(requirement.Permission))
        {
            return;
        }

        var authenticatedIdentity = context.User.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated);
        var userId = authenticatedIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? default;
        var hasDirectPermission = await dbContext.UserClaims
            .AsNoTracking()
            .AnyAsync(
                claim => claim.UserId == userId
                    && claim.ClaimType == PermissionClaim.Type
                    && claim.ClaimValue == requirement.Permission,
                cancellationToken);

        if (hasDirectPermission)
        {
            context.Succeed(requirement);
            return;
        }

        var hasRolePermission = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join roleClaim in dbContext.RoleClaims.AsNoTracking() on role.Id equals roleClaim.RoleId
            where userRole.UserId == userId
                && roleClaim.ClaimType == PermissionClaim.Type
                && roleClaim.ClaimValue == requirement.Permission
            select roleClaim.Id)
            .AnyAsync(cancellationToken);

        if (hasRolePermission)
        {
            context.Succeed(requirement);
        }
    }
}
