namespace BaseMVC.Application.Users;

public interface IUserManagementService
{
    Task<UserSearchResponse> SearchAsync(string? search, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssignableRole>> GetAssignableRolesAsync(bool canManageAccess, CancellationToken cancellationToken);
    Task<UserMutationResult> CreateAsync(string email, string password, IReadOnlyCollection<string> roleIds, string? actorUserId, bool canManageAccess, CancellationToken cancellationToken);
    Task<UserMutationResult> EditAsync(string id, string email, IReadOnlyCollection<string> roleIds, bool isLocked, string? actorUserId, bool canManageAccess, CancellationToken cancellationToken);
    Task<UserMutationResult> DeleteAsync(string id, string? actorUserId, CancellationToken cancellationToken);
}
