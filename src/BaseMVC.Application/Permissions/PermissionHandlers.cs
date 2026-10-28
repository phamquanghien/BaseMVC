using MediatR;

namespace BaseMVC.Application.Permissions;

public sealed class GetPermissionRolesHandler(IPermissionManagementService service) : IRequestHandler<GetPermissionRolesQuery, IReadOnlyList<PermissionRole>>
{
    public Task<IReadOnlyList<PermissionRole>> Handle(GetPermissionRolesQuery request, CancellationToken cancellationToken) => service.GetRolesAsync(cancellationToken);
}

public sealed class GetRolePermissionsHandler(IPermissionManagementService service) : IRequestHandler<GetRolePermissionsQuery, RolePermissions?>
{
    public Task<RolePermissions?> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken) => service.GetRolePermissionsAsync(request.RoleId, cancellationToken);
}

public sealed class UpdateRolePermissionsHandler(IPermissionManagementService service) : IRequestHandler<UpdateRolePermissionsCommand, PermissionUpdateResult>
{
    public Task<PermissionUpdateResult> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken) =>
        service.UpdateRolePermissionsAsync(request.RoleId, request.Permissions, request.ActorUserId, cancellationToken);
}
