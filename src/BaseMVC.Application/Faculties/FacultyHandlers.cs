using MediatR;

namespace BaseMVC.Application.Faculties;

public sealed class SearchFacultiesHandler(IFacultyManagementService service) : IRequestHandler<SearchFacultiesQuery, FacultySearchResponse>
{
    public Task<FacultySearchResponse> Handle(SearchFacultiesQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class CreateFacultyHandler(IFacultyManagementService service) : IRequestHandler<CreateFacultyCommand, FacultyOperationResult>
{
    public Task<FacultyOperationResult> Handle(CreateFacultyCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.FacultyCode, request.FacultyName, request.ActorUserId, cancellationToken);
}

public sealed class EditFacultyHandler(IFacultyManagementService service) : IRequestHandler<EditFacultyCommand, FacultyOperationResult>
{
    public Task<FacultyOperationResult> Handle(EditFacultyCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.FacultyCode, request.FacultyName, request.ActorUserId, cancellationToken);
}

public sealed class DeleteFacultyHandler(IFacultyManagementService service) : IRequestHandler<DeleteFacultyCommand, FacultyOperationResult>
{
    public Task<FacultyOperationResult> Handle(DeleteFacultyCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}
