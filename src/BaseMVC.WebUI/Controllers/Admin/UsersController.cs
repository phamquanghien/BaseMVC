using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Users")]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IAuthorizationService authorizationService,
    ILogger<UsersController> logger) : Controller
{
    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Users/Index.cshtml");

    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<UserSearchResponse>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var users = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            users = users.Where(user => user.Email != null && user.Email.Contains(searchTerm));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var pageUsers = await users
            .OrderBy(user => user.NormalizedEmail)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.LockoutEnabled,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var userIds = pageUsers.Select(user => user.Id).ToArray();
        var roleAssignments = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            orderby role.NormalizedName
            select new { userRole.UserId, RoleName = role.Name })
            .ToListAsync(cancellationToken);
        var rolesByUser = roleAssignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(assignment => assignment.RoleName ?? string.Empty)
                    .ToArray());

        var now = DateTimeOffset.UtcNow;
        return Ok(new UserSearchResponse(
            pageUsers.Select(user => new UserListItemViewModel(
                user.Id,
                user.Email ?? string.Empty,
                user.LockoutEnabled && user.LockoutEnd > now,
            string.Equals(user.Id, userManager.GetUserId(User), StringComparison.Ordinal),
            rolesByUser.GetValueOrDefault(user.Id, [])))
            .ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<AssignableRoleViewModel>>> GetAssignableRoles(
        CancellationToken cancellationToken)
    {
        var canManageAccess = (await authorizationService.AuthorizeAsync(
            User,
            resource: null,
            "Permissions.Access.Manage")).Succeeded;
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.NormalizedName)
            .ThenBy(role => role.Id)
            .ToListAsync(cancellationToken);

        var result = roles
            .Select(role => new AssignableRoleViewModel(
                role.Id,
                role.Name ?? string.Empty,
                canManageAccess || !IdentitySeedRoles.All.Contains(
                    role.Name,
                    StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return Ok(result);
    }

    [Authorize(Policy = "Permissions.Users.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromBody] UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var roleValidation = await ValidateRoleAssignmentsAsync(request.RoleIds);
        if (roleValidation is not null)
        {
            return roleValidation;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            logger.LogWarning(
                "Could not create user {Email}: {Errors}",
                email,
                string.Join("; ", createResult.Errors.Select(error => error.Code)));
            return BadRequest(new { errors = createResult.Errors.Select(error => error.Description) });
        }

        var assigned = await userManager.AddToRolesAsync(user, await GetRoleNamesAsync(request.RoleIds));
        if (!assigned.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { errors = assigned.Errors.Select(error => error.Description) });
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.UserCreated,
            AuditOutcome.Succeeded,
            actorUserId: userManager.GetUserId(User),
            targetUserId: user.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(Search), new { search = email }, await ToListItemAsync(user));
    }

    [Authorize(Policy = "Permissions.Users.Update")]
    [HttpPost("{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [FromBody] UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy tài khoản." } });
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var roleValidation = await ValidateRoleAssignmentsAsync(request.RoleIds, currentRoles);
        if (roleValidation is not null)
        {
            return roleValidation;
        }

        var isCurrentUser = string.Equals(
            user.Id,
            userManager.GetUserId(User),
            StringComparison.Ordinal);
        if (isCurrentUser && request.IsLocked)
        {
            return Conflict(new { errors = new[] { "Bạn không thể tự khóa tài khoản đang sử dụng." } });
        }

        var desiredRoles = await GetRoleNamesAsync(request.RoleIds);
        var removesSuperAdmin = currentRoles.Contains(
            IdentitySeedRoles.SuperAdmin,
            StringComparer.OrdinalIgnoreCase)
            && !desiredRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        if (removesSuperAdmin || request.IsLocked)
        {
            var superAdmins = await userManager.GetUsersInRoleAsync(IdentitySeedRoles.SuperAdmin);
            if (superAdmins.Count == 1 && superAdmins[0].Id == user.Id)
            {
                return Conflict(new { errors = new[] { "Không thể khóa hoặc gỡ role của SuperAdmin cuối cùng." } });
            }
        }

        var newEmail = request.Email.Trim();
        if (currentRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                errors = new[] { "Không thể thay đổi email của tài khoản có role SuperAdmin." }
            });
        }

        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await userManager.FindByEmailAsync(newEmail);
            if (existingUser is not null && existingUser.Id != user.Id)
            {
                return Conflict(new { errors = new[] { "Email này đã được sử dụng." } });
            }

            user.Email = newEmail;
            user.UserName = newEmail;
            user.EmailConfirmed = true;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { errors = updateResult.Errors.Select(error => error.Description) });
        }

        var removeResult = await userManager.RemoveFromRolesAsync(
            user,
            currentRoles.Except(desiredRoles, StringComparer.OrdinalIgnoreCase));
        if (!removeResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { errors = removeResult.Errors.Select(error => error.Description) });
        }

        var addResult = await userManager.AddToRolesAsync(
            user,
            desiredRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase));
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { errors = addResult.Errors.Select(error => error.Description) });
        }

        if (request.IsLocked)
        {
            var lockoutEnabled = await userManager.SetLockoutEnabledAsync(user, true);
            if (!lockoutEnabled.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(new { errors = lockoutEnabled.Errors.Select(error => error.Description) });
            }

            var lockoutResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!lockoutResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(new { errors = lockoutResult.Errors.Select(error => error.Description) });
            }
        }
        else if (user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
            if (!lockoutResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(new { errors = lockoutResult.Errors.Select(error => error.Description) });
            }
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.UserUpdated,
            AuditOutcome.Succeeded,
            actorUserId: userManager.GetUserId(User),
            targetUserId: user.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(await ToListItemAsync(user));
    }

    [Authorize(Policy = "Permissions.Users.Delete")]
    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (string.Equals(id, userManager.GetUserId(User), StringComparison.Ordinal))
        {
            return Conflict(new { errors = new[] { "Bạn không thể tự xóa tài khoản đang sử dụng." } });
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(new { errors = new[] { "Không tìm thấy tài khoản." } });
        }

        var adminUsers = await userManager.GetUsersInRoleAsync(IdentitySeedRoles.SuperAdmin);
        if (adminUsers.Count == 1 && adminUsers[0].Id == user.Id)
        {
            return Conflict(new { errors = new[] { "Không thể xóa SuperAdmin cuối cùng." } });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                "Could not delete user {UserId}: {Errors}",
                id,
                string.Join("; ", result.Errors.Select(error => error.Code)));
            return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.UserDeleted,
            AuditOutcome.Succeeded,
            actorUserId: userManager.GetUserId(User),
            targetUserId: user.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult?> ValidateRoleAssignmentsAsync(
        IReadOnlyCollection<string> roleIds,
        IEnumerable<string>? existingRoleNames = null)
    {
        var uniqueRoleIds = roleIds.Distinct(StringComparer.Ordinal).ToArray();
        var selectedRoles = await roleManager.Roles
            .AsNoTracking()
            .Where(role => uniqueRoleIds.Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToListAsync();

        if (selectedRoles.Count != uniqueRoleIds.Length)
        {
            return BadRequest(new { errors = new[] { "Danh sách chứa role không hợp lệ." } });
        }

        var selectedRoleNames = selectedRoles
            .Select(role => role.Name ?? string.Empty)
            .ToArray();
        var changesSystemRole = IdentitySeedRoles.All.Any(roleName =>
            selectedRoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase)
            != (existingRoleNames?.Contains(roleName, StringComparer.OrdinalIgnoreCase) ?? false));
        if (!changesSystemRole)
        {
            return null;
        }

        var authorization = await authorizationService.AuthorizeAsync(
            User,
            resource: null,
            "Permissions.Access.Manage");
        return authorization.Succeeded
            ? null
            : StatusCode(
                StatusCodes.Status403Forbidden,
                new { errors = new[] { "Cần quyền quản lý phân quyền để gán role hệ thống." } });
    }

    private async Task<string[]> GetRoleNamesAsync(IReadOnlyCollection<string> roleIds)
    {
        var uniqueRoleIds = roleIds.Distinct(StringComparer.Ordinal).ToArray();
        return await roleManager.Roles
            .AsNoTracking()
            .Where(role => uniqueRoleIds.Contains(role.Id))
            .Select(role => role.Name ?? string.Empty)
            .ToArrayAsync();
    }

    private async Task<UserListItemViewModel> ToListItemAsync(ApplicationUser user)
    {
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
        return new UserListItemViewModel(
            user.Id,
            user.Email ?? string.Empty,
            lockoutEnd > DateTimeOffset.UtcNow,
            string.Equals(user.Id, userManager.GetUserId(User), StringComparison.Ordinal),
            (await userManager.GetRolesAsync(user)).ToArray());
    }
}
