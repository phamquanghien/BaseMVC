using MediatR;

namespace BaseMVC.Application.Users;

public sealed record SearchUsersQuery(string? Search, int Page, int PageSize, string? CurrentUserId) : IRequest<UserSearchResponse>;
public sealed record GetAssignableRolesQuery(bool CanManageAccess) : IRequest<IReadOnlyList<AssignableRole>>;
public sealed record CreateUserCommand(string Email, string Password, IReadOnlyCollection<string> RoleIds, string? ActorUserId, bool CanManageAccess) : IRequest<UserMutationResult>;
public sealed record EditUserCommand(string Id, string Email, IReadOnlyCollection<string> RoleIds, bool IsLocked, string? ActorUserId, bool CanManageAccess) : IRequest<UserMutationResult>;
public sealed record DeleteUserCommand(string Id, string? ActorUserId) : IRequest<UserMutationResult>;
