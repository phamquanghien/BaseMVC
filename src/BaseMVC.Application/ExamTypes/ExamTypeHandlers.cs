using MediatR;

namespace BaseMVC.Application.ExamTypes;

public sealed class SearchExamTypesHandler(IExamTypeManagementService service) : IRequestHandler<SearchExamTypesQuery, ExamTypeSearchResponse>
{
    public Task<ExamTypeSearchResponse> Handle(SearchExamTypesQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class CreateExamTypeHandler(IExamTypeManagementService service) : IRequestHandler<CreateExamTypeCommand, ExamTypeOperationResult>
{
    public Task<ExamTypeOperationResult> Handle(CreateExamTypeCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.Name, request.ActorUserId, cancellationToken);
}

public sealed class EditExamTypeHandler(IExamTypeManagementService service) : IRequestHandler<EditExamTypeCommand, ExamTypeOperationResult>
{
    public Task<ExamTypeOperationResult> Handle(EditExamTypeCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.Name, request.ActorUserId, cancellationToken);
}

public sealed class DeleteExamTypeHandler(IExamTypeManagementService service) : IRequestHandler<DeleteExamTypeCommand, ExamTypeOperationResult>
{
    public Task<ExamTypeOperationResult> Handle(DeleteExamTypeCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}
