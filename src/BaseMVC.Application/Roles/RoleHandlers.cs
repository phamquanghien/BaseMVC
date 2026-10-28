using MediatR;

namespace BaseMVC.Application.Roles;

public sealed class SearchRolesHandler(IRoleManagementService service) : IRequestHandler<SearchRolesQuery, RoleSearchResponse>
{
    public Task<RoleSearchResponse> Handle(SearchRolesQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class CreateRoleHandler(IRoleManagementService service) : IRequestHandler<CreateRoleCommand, RoleOperationResult>
{
    public Task<RoleOperationResult> Handle(CreateRoleCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.Name, request.ActorUserId, cancellationToken);
}

public sealed class EditRoleHandler(IRoleManagementService service) : IRequestHandler<EditRoleCommand, RoleOperationResult>
{
    public Task<RoleOperationResult> Handle(EditRoleCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.Name, request.ActorUserId, cancellationToken);
}

public sealed class DeleteRoleHandler(IRoleManagementService service) : IRequestHandler<DeleteRoleCommand, RoleOperationResult>
{
    public Task<RoleOperationResult> Handle(DeleteRoleCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}
