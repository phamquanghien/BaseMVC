using BaseMVC.Application.Users;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Identity;

public sealed class UserManagementService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<UserSearchResponse> SearchAsync(string? search, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var users = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users.Where(user => user.Email != null && user.Email.Contains(term));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var pageUsers = await users.OrderBy(user => user.NormalizedEmail).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new { user.Id, user.Email, user.LockoutEnabled, user.LockoutEnd })
            .ToListAsync(cancellationToken);
        var ids = pageUsers.Select(user => user.Id).ToArray();
        var assignments = await (from userRole in dbContext.UserRoles.AsNoTracking()
                                 join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                                 where ids.Contains(userRole.UserId)
                                 orderby role.NormalizedName
                                 select new { userRole.UserId, Name = role.Name }).ToListAsync(cancellationToken);
        var roles = assignments.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.Name ?? string.Empty).ToArray());
        var now = DateTimeOffset.UtcNow;
        return new UserSearchResponse(pageUsers.Select(user => new UserListItem(
            user.Id, user.Email ?? string.Empty, user.LockoutEnabled && user.LockoutEnd > now,
            string.Equals(user.Id, currentUserId, StringComparison.Ordinal), roles.GetValueOrDefault(user.Id, [])))
            .ToArray(), page, pageSize, totalCount, totalPages);
    }

    public async Task<IReadOnlyList<AssignableRole>> GetAssignableRolesAsync(bool canManageAccess, CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles.AsNoTracking().OrderBy(role => role.NormalizedName).ThenBy(role => role.Id).ToListAsync(cancellationToken);
        return roles.Select(role => new AssignableRole(role.Id, role.Name ?? string.Empty,
            canManageAccess || !IdentitySeedRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase))).ToArray();
    }

    public async Task<UserMutationResult> CreateAsync(string email, string password, IReadOnlyCollection<string> roleIds, string? actorUserId, bool canManageAccess, CancellationToken cancellationToken)
    {
        email = email.Trim();
        var validatedRoles = await ValidateRoleAssignmentsAsync(roleIds, null, canManageAccess, cancellationToken);
        if (validatedRoles.Error is not null) return validatedRoles.Error;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true };
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogWarning("Could not create user {Email}: {Errors}", email, string.Join("; ", created.Errors.Select(error => error.Code)));
            return UserMutationResult.BadRequest(created.Errors.Select(error => error.Description));
        }
        var assigned = await userManager.AddToRolesAsync(user, validatedRoles.Names!);
        if (!assigned.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UserMutationResult.BadRequest(assigned.Errors.Select(error => error.Description));
        }
        await auditLogWriter.WriteAsync(AuditEventType.UserCreated, AuditOutcome.Succeeded, actorUserId, user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserMutationResult.Created(await ToListItemAsync(user, actorUserId));
    }

    public async Task<UserMutationResult> EditAsync(string id, string email, IReadOnlyCollection<string> roleIds, bool isLocked, string? actorUserId, bool canManageAccess, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return UserMutationResult.NotFound("Không tìm thấy tài khoản.");
        var currentRoles = await userManager.GetRolesAsync(user);
        var validatedRoles = await ValidateRoleAssignmentsAsync(roleIds, currentRoles, canManageAccess, cancellationToken);
        if (validatedRoles.Error is not null) return validatedRoles.Error;
        var isCurrentUser = string.Equals(user.Id, actorUserId, StringComparison.Ordinal);
        if (isCurrentUser && isLocked) return UserMutationResult.Conflict("Bạn không thể tự khóa tài khoản đang sử dụng.");
        var desiredRoles = validatedRoles.Names!;
        var removesSuperAdmin = currentRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            && !desiredRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var locksSuperAdmin = currentRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase) && isLocked;
        if ((removesSuperAdmin || locksSuperAdmin) && !await HasOtherUnlockedSuperAdminAsync(user.Id))
            return UserMutationResult.Conflict("Không thể khóa hoặc gỡ role của SuperAdmin có thể đăng nhập cuối cùng.");
        var newEmail = email.Trim();
        if (currentRoles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            return UserMutationResult.Conflict("Không thể thay đổi email của tài khoản có role SuperAdmin.");
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await userManager.FindByEmailAsync(newEmail);
            if (existingUser is not null && existingUser.Id != user.Id) return UserMutationResult.Conflict("Email này đã được sử dụng.");
            user.Email = newEmail; user.UserName = newEmail; user.EmailConfirmed = true;
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(update.Errors.Select(error => error.Description)); }
        var removed = await userManager.RemoveFromRolesAsync(user, currentRoles.Except(desiredRoles, StringComparer.OrdinalIgnoreCase));
        if (!removed.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(removed.Errors.Select(error => error.Description)); }
        var added = await userManager.AddToRolesAsync(user, desiredRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase));
        if (!added.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(added.Errors.Select(error => error.Description)); }
        if (isLocked)
        {
            var enabled = await userManager.SetLockoutEnabledAsync(user, true);
            if (!enabled.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(enabled.Errors.Select(error => error.Description)); }
            var locked = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            if (!locked.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(locked.Errors.Select(error => error.Description)); }
        }
        else if (user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var unlocked = await userManager.SetLockoutEndDateAsync(user, null);
            if (!unlocked.Succeeded) { await transaction.RollbackAsync(cancellationToken); return UserMutationResult.BadRequest(unlocked.Errors.Select(error => error.Description)); }
        }
        await auditLogWriter.WriteAsync(AuditEventType.UserUpdated, AuditOutcome.Succeeded, actorUserId, user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserMutationResult.Success(await ToListItemAsync(user, actorUserId));
    }

    public async Task<UserMutationResult> DeleteAsync(string id, string? actorUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(id, actorUserId, StringComparison.Ordinal)) return UserMutationResult.Conflict("Bạn không thể tự xóa tài khoản đang sử dụng.");
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return UserMutationResult.NotFound("Không tìm thấy tài khoản.");
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(IdentitySeedRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase) && !await HasOtherUnlockedSuperAdminAsync(user.Id))
            return UserMutationResult.Conflict("Không thể xóa SuperAdmin có thể đăng nhập cuối cùng.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var deleted = await userManager.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning("Could not delete user {UserId}: {Errors}", id, string.Join("; ", deleted.Errors.Select(error => error.Code)));
            return UserMutationResult.BadRequest(deleted.Errors.Select(error => error.Description));
        }
        await auditLogWriter.WriteAsync(AuditEventType.UserDeleted, AuditOutcome.Succeeded, actorUserId, user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserMutationResult.NoContent();
    }

    private async Task<(string[]? Names, UserMutationResult? Error)> ValidateRoleAssignmentsAsync(IReadOnlyCollection<string> roleIds, IEnumerable<string>? currentNames, bool canManageAccess, CancellationToken cancellationToken)
    {
        var uniqueIds = roleIds.Distinct(StringComparer.Ordinal).ToArray();
        var roles = await roleManager.Roles.AsNoTracking().Where(role => uniqueIds.Contains(role.Id)).Select(role => new { role.Id, role.Name }).ToListAsync(cancellationToken);
        if (roles.Count != uniqueIds.Length) return (null, UserMutationResult.BadRequest(["Danh sách chứa role không hợp lệ."]));
        var names = roles.Select(role => role.Name ?? string.Empty).ToArray();
        var changesSystemRole = IdentitySeedRoles.All.Any(name => names.Contains(name, StringComparer.OrdinalIgnoreCase) != (currentNames?.Contains(name, StringComparer.OrdinalIgnoreCase) ?? false));
        if (changesSystemRole && !canManageAccess) return (null, UserMutationResult.Forbidden("Cần quyền quản lý phân quyền để gán role hệ thống."));
        return (names, null);
    }

    private async Task<UserListItem> ToListItemAsync(ApplicationUser user, string? currentUserId)
    {
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
        return new UserListItem(user.Id, user.Email ?? string.Empty, lockoutEnd > DateTimeOffset.UtcNow,
            string.Equals(user.Id, currentUserId, StringComparison.Ordinal), (await userManager.GetRolesAsync(user)).ToArray());
    }

    private async Task<bool> HasOtherUnlockedSuperAdminAsync(string excludedUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var admins = await userManager.GetUsersInRoleAsync(IdentitySeedRoles.SuperAdmin);
        return admins.Any(user => user.Id != excludedUserId && (user.LockoutEnd is null || user.LockoutEnd <= now));
    }
}
