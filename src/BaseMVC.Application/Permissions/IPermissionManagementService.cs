namespace BaseMVC.Application.Permissions;

public interface IPermissionManagementService
{
    Task<IReadOnlyList<PermissionRole>> GetRolesAsync(CancellationToken cancellationToken);
    Task<RolePermissions?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken);
    Task<PermissionUpdateResult> UpdateRolePermissionsAsync(string roleId, IReadOnlyCollection<string>? permissions, string? actorUserId, CancellationToken cancellationToken);
}
