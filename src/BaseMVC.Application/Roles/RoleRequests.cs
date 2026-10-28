using MediatR;

namespace BaseMVC.Application.Roles;

public sealed record SearchRolesQuery(string? Search, int Page, int PageSize) : IRequest<RoleSearchResponse>;
public sealed record CreateRoleCommand(string Name, string? ActorUserId) : IRequest<RoleOperationResult>;
public sealed record EditRoleCommand(string Id, string Name, string? ActorUserId) : IRequest<RoleOperationResult>;
public sealed record DeleteRoleCommand(string Id, string? ActorUserId) : IRequest<RoleOperationResult>;
