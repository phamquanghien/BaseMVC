namespace BaseMVC.Application.Roles;

public interface IRoleManagementService
{
    Task<RoleSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<RoleOperationResult> CreateAsync(string name, string? actorUserId, CancellationToken cancellationToken);
    Task<RoleOperationResult> EditAsync(string id, string name, string? actorUserId, CancellationToken cancellationToken);
    Task<RoleOperationResult> DeleteAsync(string id, string? actorUserId, CancellationToken cancellationToken);
}
