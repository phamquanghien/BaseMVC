using MediatR;

namespace BaseMVC.Application.Permissions;

public sealed record GetPermissionRolesQuery : IRequest<IReadOnlyList<PermissionRole>>;
public sealed record GetRolePermissionsQuery(string RoleId) : IRequest<RolePermissions?>;
public sealed record UpdateRolePermissionsCommand(string RoleId, IReadOnlyCollection<string>? Permissions, string? ActorUserId) : IRequest<PermissionUpdateResult>;
