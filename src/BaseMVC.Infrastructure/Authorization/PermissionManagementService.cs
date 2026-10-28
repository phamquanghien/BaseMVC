using System.Security.Claims;
using BaseMVC.Application.Permissions;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Authorization;

public sealed class PermissionManagementService(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ILogger<PermissionManagementService> logger) : IPermissionManagementService
{
    public async Task<IReadOnlyList<PermissionRole>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles.AsNoTracking().OrderBy(role => role.NormalizedName).ThenBy(role => role.Id)
            .Select(role => new { role.Id, role.Name }).ToListAsync(cancellationToken);
        return roles.Select(role => new PermissionRole(role.Id, role.Name ?? string.Empty)).ToArray();
    }

    public async Task<RolePermissions?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null) return null;
        var granted = (await dbContext.RoleClaims.AsNoTracking()
            .Where(claim => claim.RoleId == roleId && claim.ClaimType == PermissionClaim.Type && claim.ClaimValue != null)
            .Select(claim => claim.ClaimValue!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        return new RolePermissions(role.Id, role.Name ?? string.Empty, PermissionCatalog.All.Select(permission =>
            new PermissionItem(permission.Name, permission.Module, permission.Action, granted.Contains(permission.Name))).ToArray());
    }

    public async Task<PermissionUpdateResult> UpdateRolePermissionsAsync(string roleId, IReadOnlyCollection<string>? permissions, string? actorUserId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null) return PermissionUpdateResult.NotFound("Không tìm thấy role.");
        if (permissions is null) return PermissionUpdateResult.BadRequest("Danh sách quyền không hợp lệ.");
        var known = PermissionCatalog.All.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var selected = permissions.ToHashSet(StringComparer.Ordinal);
        if (selected.Except(known).Any()) return PermissionUpdateResult.BadRequest("Danh sách chứa quyền không có trong catalog.");
        var isSuperAdmin = string.Equals(role.Name, IdentitySeedRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
        var required = new[] { PermissionClaim.Create("Access", "Read"), PermissionClaim.Create("Access", "Manage") };
        if (isSuperAdmin && required.Any(permission => !selected.Contains(permission)))
            return PermissionUpdateResult.Conflict("Role SuperAdmin bắt buộc phải giữ quyền xem và quản lý phân quyền.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var currentClaims = await roleManager.GetClaimsAsync(role);
        var catalogClaims = currentClaims.Where(claim => claim.Type == PermissionClaim.Type && known.Contains(claim.Value)).ToArray();
        var current = catalogClaims.Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in current.Except(selected))
        {
            foreach (var claim in catalogClaims.Where(claim => claim.Value == permission))
            {
                var removed = await roleManager.RemoveClaimAsync(role, claim);
                if (!removed.Succeeded)
                    throw new InvalidOperationException($"Could not revoke permission '{permission}' from role '{role.Name}': {FormatErrors(removed)}");
            }
        }
        foreach (var permission in selected.Except(current))
        {
            var added = await roleManager.AddClaimAsync(role, new Claim(PermissionClaim.Type, permission));
            if (!added.Succeeded)
                throw new InvalidOperationException($"Could not grant permission '{permission}' to role '{role.Name}': {FormatErrors(added)}");
        }
        await auditLogWriter.WriteAsync(AuditEventType.RolePermissionsUpdated, AuditOutcome.Succeeded, actorUserId, targetRoleId: role.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated catalog permissions for role {RoleName}; {PermissionCount} permissions granted.", role.Name, selected.Count);
        var result = await GetRolePermissionsAsync(roleId, cancellationToken);
        return PermissionUpdateResult.Success(result!);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
