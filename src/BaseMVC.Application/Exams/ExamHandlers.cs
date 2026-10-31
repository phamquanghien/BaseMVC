using MediatR;

namespace BaseMVC.Application.Exams;

public sealed class SearchExamsHandler(IExamManagementService service) : IRequestHandler<SearchExamsQuery, ExamSearchResponse>
{
    public Task<ExamSearchResponse> Handle(SearchExamsQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request.Search, request.ExamTypeId, request.Status, request.IncludeDeleted, request.Page, request.PageSize, cancellationToken);
}

public sealed class ListExamTypeOptionsHandler(IExamManagementService service) : IRequestHandler<ListExamTypeOptionsQuery, IReadOnlyList<ExamTypeOption>>
{
    public Task<IReadOnlyList<ExamTypeOption>> Handle(ListExamTypeOptionsQuery request, CancellationToken cancellationToken) =>
        service.ListExamTypeOptionsAsync(cancellationToken);
}

public sealed class CreateExamHandler(IExamManagementService service) : IRequestHandler<CreateExamCommand, ExamOperationResult>
{
    public Task<ExamOperationResult> Handle(CreateExamCommand request, CancellationToken cancellationToken) =>
        service.CreateAsync(request.Name, request.ExamTypeId, request.ActorUserId, cancellationToken);
}

public sealed class EditExamHandler(IExamManagementService service) : IRequestHandler<EditExamCommand, ExamOperationResult>
{
    public Task<ExamOperationResult> Handle(EditExamCommand request, CancellationToken cancellationToken) =>
        service.EditAsync(request.Id, request.Name, request.ExamTypeId, request.ActorUserId, cancellationToken);
}

public sealed class DeleteExamHandler(IExamManagementService service) : IRequestHandler<DeleteExamCommand, ExamOperationResult>
{
    public Task<ExamOperationResult> Handle(DeleteExamCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request.Id, request.ActorUserId, cancellationToken);
}

public sealed class ChangeExamStatusHandler(IExamManagementService service) : IRequestHandler<ChangeExamStatusCommand, ExamOperationResult>
{
    public Task<ExamOperationResult> Handle(ChangeExamStatusCommand request, CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(request.Id, request.Status, request.ActorUserId, cancellationToken);
}
