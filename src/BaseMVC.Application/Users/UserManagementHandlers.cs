using MediatR;

namespace BaseMVC.Application.Users;

public sealed class SearchUsersHandler(IUserManagementService service) : IRequestHandler<SearchUsersQuery, UserSearchResponse>
{
    public Task<UserSearchResponse> Handle(SearchUsersQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.Page, request.PageSize, request.CurrentUserId, cancellationToken);
}

public sealed class GetAssignableRolesHandler(IUserManagementService service) : IRequestHandler<GetAssignableRolesQuery, IReadOnlyList<AssignableRole>>
{
    public Task<IReadOnlyList<AssignableRole>> Handle(GetAssignableRolesQuery request, CancellationToken cancellationToken) =>
        service.GetAssignableRolesAsync(request.CanManageAccess, cancellationToken);
}

public sealed class CreateUserHandler(IUserManagementService service) : IRequestHandler<CreateUserCommand, UserMutationResult>
{
    public Task<UserMutationResult> Handle(CreateUserCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.Email, request.Password, request.RoleIds, request.ActorUserId, request.CanManageAccess, cancellationToken);
}

public sealed class EditUserHandler(IUserManagementService service) : IRequestHandler<EditUserCommand, UserMutationResult>
{
    public Task<UserMutationResult> Handle(EditUserCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.Email, request.RoleIds, request.IsLocked, request.ActorUserId, request.CanManageAccess, cancellationToken);
}

public sealed class DeleteUserHandler(IUserManagementService service) : IRequestHandler<DeleteUserCommand, UserMutationResult>
{
    public Task<UserMutationResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}
