using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Models.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Roles")]
public sealed class RolesController(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ILogger<RolesController> logger) : Controller
{
    [Authorize(Policy = "Permissions.Roles.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Roles/Index.cshtml");

    [Authorize(Policy = "Permissions.Roles.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<RoleSearchResponse>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var roles = roleManager.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            roles = roles.Where(role => role.Name != null && role.Name.Contains(searchTerm));
        }

        var totalCount = await roles.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        var pageRoles = await roles
            .OrderBy(role => role.NormalizedName)
            .ThenBy(role => role.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(role => new { role.Id, role.Name })
            .ToListAsync(cancellationToken);
        var roleIds = pageRoles.Select(role => role.Id).ToArray();
        var userCounts = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => roleIds.Contains(userRole.RoleId))
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RoleId, item => item.Count, cancellationToken);

        return Ok(new RoleSearchResponse(
            pageRoles
                .Select(role => new RoleListItemViewModel(
                    role.Id,
                    role.Name ?? string.Empty,
                    userCounts.GetValueOrDefault(role.Id)))
                .ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    [Authorize(Policy = "Permissions.Roles.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromBody] RoleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { errors = new[] { "Tên role không được để trống." } });
        }

        if (await roleManager.RoleExistsAsync(name))
        {
            return Conflict(new { errors = new[] { "Role này đã tồn tại." } });
        }

        var role = new IdentityRole(name);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Could not create role {RoleName}: {Errors}",
                name,
                string.Join("; ", result.Errors.Select(error => error.Code)));
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.RoleCreated,
            AuditOutcome.Succeeded,
            actorUserId: User.FindFirstValue(ClaimTypes.NameIdentifier),
            targetRoleId: role.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(Search), new { search = name }, new RoleListItemViewModel(role.Id, name, 0));
    }

    [Authorize(Policy = "Permissions.Roles.Update")]
    [HttpPost("{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [FromBody] RoleMutationRequest request,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy role." } });
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { errors = new[] { "Tên role không được để trống." } });
        }

        if (IdentitySeedRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
        {
            return Conflict(new { errors = new[] { "Không thể đổi tên role hệ thống." } });
        }

        if (!string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)
            && await roleManager.RoleExistsAsync(name))
        {
            return Conflict(new { errors = new[] { "Role này đã tồn tại." } });
        }

        role.Name = name;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Could not update role {RoleId}: {Errors}",
                id,
                string.Join("; ", result.Errors.Select(error => error.Code)));
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.RoleUpdated,
            AuditOutcome.Succeeded,
            actorUserId: User.FindFirstValue(ClaimTypes.NameIdentifier),
            targetRoleId: role.Id,
            cancellationToken: cancellationToken);
        var userCount = await dbContext.UserRoles
            .AsNoTracking()
            .CountAsync(userRole => userRole.RoleId == id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Ok(new RoleListItemViewModel(role.Id, role.Name, userCount));
    }

    [Authorize(Policy = "Permissions.Roles.Delete")]
    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy role." } });
        }

        if (IdentitySeedRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
        {
            return Conflict(new { errors = new[] { "Không thể xóa role hệ thống." } });
        }

        var userCount = await dbContext.UserRoles
            .AsNoTracking()
            .CountAsync(userRole => userRole.RoleId == id);

        if (userCount > 0)
        {
            return Conflict(new
            {
                errors = new[] { $"Không thể xóa role đang được gán cho {userCount} tài khoản." }
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                "Could not delete role {RoleId}: {Errors}",
                id,
                string.Join("; ", result.Errors.Select(error => error.Code)));
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.RoleDeleted,
            AuditOutcome.Succeeded,
            actorUserId: User.FindFirstValue(ClaimTypes.NameIdentifier),
            targetRoleId: role.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
