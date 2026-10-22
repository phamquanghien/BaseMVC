using System.Security.Claims;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Models.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Permissions")]
public sealed class PermissionsController(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    ILogger<PermissionsController> logger) : Controller
{
    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Permissions/Index.cshtml");

    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.NormalizedName)
            .ThenBy(role => role.Id)
            .Select(role => new { role.Id, role.Name })
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("roles/{roleId}")]
    public async Task<ActionResult<RolePermissionsViewModel>> GetRolePermissions(
        string roleId,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);

        if (role is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy role." } });
        }

        var grantedPermissions = await dbContext.RoleClaims
            .AsNoTracking()
            .Where(claim => claim.RoleId == roleId
                && claim.ClaimType == PermissionClaim.Type
                && claim.ClaimValue != null)
            .Select(claim => claim.ClaimValue!)
            .ToListAsync(cancellationToken);
        var granted = grantedPermissions.ToHashSet(StringComparer.Ordinal);

        return Ok(new RolePermissionsViewModel(
            role.Id,
            role.Name ?? string.Empty,
            PermissionCatalog.All
                .Select(permission => new PermissionViewModel(
                    permission.Name,
                    permission.Module,
                    permission.Action,
                    granted.Contains(permission.Name)))
                .ToArray()));
    }

    [Authorize(Policy = "Permissions.Access.Manage")]
    [HttpPost("roles/{roleId}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<RolePermissionsViewModel>> UpdateRolePermissions(
        string roleId,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId);

        if (role is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy role." } });
        }

        if (request.Permissions is null)
        {
            return BadRequest(new { errors = new[] { "Danh sách quyền không hợp lệ." } });
        }

        var knownPermissions = PermissionCatalog.All
            .Select(permission => permission.Name)
            .ToHashSet(StringComparer.Ordinal);
        var selectedPermissions = request.Permissions.ToHashSet(StringComparer.Ordinal);
        var unknownPermissions = selectedPermissions.Except(knownPermissions).ToArray();

        if (unknownPermissions.Length > 0)
        {
            return BadRequest(new { errors = new[] { "Danh sách chứa quyền không có trong catalog." } });
        }

        var isSuperAdmin = string.Equals(
            role.Name,
            IdentitySeedRoles.SuperAdmin,
            StringComparison.OrdinalIgnoreCase);

        var requiredSuperAdminPermissions = new[]
        {
            PermissionClaim.Create("Access", "Read"),
            PermissionClaim.Create("Access", "Manage")
        };

        if (isSuperAdmin && requiredSuperAdminPermissions.Any(permission => !selectedPermissions.Contains(permission)))
        {
            return Conflict(new
            {
                errors = new[] { "Role SuperAdmin bắt buộc phải giữ quyền xem và quản lý phân quyền." }
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var currentClaims = await roleManager.GetClaimsAsync(role);
        var catalogClaims = currentClaims
            .Where(claim => claim.Type == PermissionClaim.Type && knownPermissions.Contains(claim.Value))
            .ToArray();
        var currentPermissions = catalogClaims
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in currentPermissions.Except(selectedPermissions))
        {
            foreach (var claim in catalogClaims.Where(claim => claim.Value == permission))
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not revoke permission '{permission}' from role '{role.Name}': {FormatErrors(removeResult)}");
                }
            }
        }

        foreach (var permission in selectedPermissions.Except(currentPermissions))
        {
            var addResult = await roleManager.AddClaimAsync(
                role,
                new Claim(PermissionClaim.Type, permission));

            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not grant permission '{permission}' to role '{role.Name}': {FormatErrors(addResult)}");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Updated catalog permissions for role {RoleName}; {PermissionCount} permissions granted.",
            role.Name,
            selectedPermissions.Count);

        return await GetRolePermissions(roleId, cancellationToken);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
