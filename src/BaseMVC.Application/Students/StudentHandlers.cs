using MediatR;

namespace BaseMVC.Application.Students;

public sealed class SearchStudentsHandler(IStudentManagementService service) : IRequestHandler<SearchStudentsQuery, StudentSearchResponse>
{
    public Task<StudentSearchResponse> Handle(SearchStudentsQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class ListFacultyOptionsHandler(IStudentManagementService service) : IRequestHandler<ListFacultyOptionsQuery, IReadOnlyList<FacultyOption>>
{
    public Task<IReadOnlyList<FacultyOption>> Handle(ListFacultyOptionsQuery request, CancellationToken cancellationToken) =>
        service.ListFacultyOptionsAsync(cancellationToken);
}

public sealed class CreateStudentHandler(IStudentManagementService service) : IRequestHandler<CreateStudentCommand, StudentOperationResult>
{
    public Task<StudentOperationResult> Handle(CreateStudentCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.Input, request.ActorUserId, cancellationToken);
}

public sealed class EditStudentHandler(IStudentManagementService service) : IRequestHandler<EditStudentCommand, StudentOperationResult>
{
    public Task<StudentOperationResult> Handle(EditStudentCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.Input, request.ActorUserId, cancellationToken);
}

public sealed class DeleteStudentHandler(IStudentManagementService service) : IRequestHandler<DeleteStudentCommand, StudentOperationResult>
{
    public Task<StudentOperationResult> Handle(DeleteStudentCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}
