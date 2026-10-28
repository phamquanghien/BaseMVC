using BaseMVC.Application.Roles;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Identity;

public sealed class RoleManagementService(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ILogger<RoleManagementService> logger) : IRoleManagementService
{
    public async Task<RoleSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var roles = roleManager.Roles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            roles = roles.Where(role => role.Name != null && role.Name.Contains(term));
        }
        var totalCount = await roles.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var pageRoles = await roles.OrderBy(role => role.NormalizedName).ThenBy(role => role.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(role => new { role.Id, role.Name }).ToListAsync(cancellationToken);
        var ids = pageRoles.Select(role => role.Id).ToArray();
        var counts = await dbContext.UserRoles.AsNoTracking().Where(userRole => ids.Contains(userRole.RoleId))
            .GroupBy(userRole => userRole.RoleId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        return new RoleSearchResponse(pageRoles.Select(role => new RoleListItem(role.Id, role.Name ?? string.Empty, counts.GetValueOrDefault(role.Id))).ToArray(), page, pageSize, totalCount, totalPages);
    }

    public async Task<RoleOperationResult> CreateAsync(string name, string? actorUserId, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return RoleOperationResult.BadRequest(["Tên role không được để trống."]);
        if (await roleManager.RoleExistsAsync(name)) return RoleOperationResult.Conflict("Role này đã tồn tại.");
        var role = new IdentityRole(name);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not create role {RoleName}: {Errors}", name, string.Join("; ", result.Errors.Select(error => error.Code)));
            return RoleOperationResult.BadRequest(result.Errors.Select(error => error.Description));
        }
        await auditLogWriter.WriteAsync(AuditEventType.RoleCreated, AuditOutcome.Succeeded, actorUserId, targetRoleId: role.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoleOperationResult.Created(new RoleListItem(role.Id, name, 0));
    }

    public async Task<RoleOperationResult> EditAsync(string id, string name, string? actorUserId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return RoleOperationResult.NotFound("Không tìm thấy role.");
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return RoleOperationResult.BadRequest(["Tên role không được để trống."]);
        if (IdentitySeedRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase)) return RoleOperationResult.Conflict("Không thể đổi tên role hệ thống.");
        if (!string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase) && await roleManager.RoleExistsAsync(name))
            return RoleOperationResult.Conflict("Role này đã tồn tại.");
        role.Name = name;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not update role {RoleId}: {Errors}", id, string.Join("; ", result.Errors.Select(error => error.Code)));
            return RoleOperationResult.BadRequest(result.Errors.Select(error => error.Description));
        }
        await auditLogWriter.WriteAsync(AuditEventType.RoleUpdated, AuditOutcome.Succeeded, actorUserId, targetRoleId: role.Id, cancellationToken: cancellationToken);
        var userCount = await dbContext.UserRoles.AsNoTracking().CountAsync(userRole => userRole.RoleId == id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoleOperationResult.Success(new RoleListItem(role.Id, role.Name, userCount));
    }

    public async Task<RoleOperationResult> DeleteAsync(string id, string? actorUserId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return RoleOperationResult.NotFound("Không tìm thấy role.");
        if (IdentitySeedRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase)) return RoleOperationResult.Conflict("Không thể xóa role hệ thống.");
        var userCount = await dbContext.UserRoles.AsNoTracking().CountAsync(userRole => userRole.RoleId == id, cancellationToken);
        if (userCount > 0) return RoleOperationResult.Conflict($"Không thể xóa role đang được gán cho {userCount} tài khoản.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning("Could not delete role {RoleId}: {Errors}", id, string.Join("; ", result.Errors.Select(error => error.Code)));
            return RoleOperationResult.BadRequest(result.Errors.Select(error => error.Description));
        }
        await auditLogWriter.WriteAsync(AuditEventType.RoleDeleted, AuditOutcome.Succeeded, actorUserId, targetRoleId: role.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoleOperationResult.NoContent();
    }
}
