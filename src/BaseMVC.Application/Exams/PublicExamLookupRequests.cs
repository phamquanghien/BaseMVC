using MediatR;

namespace BaseMVC.Application.Exams;

public sealed record ListPublicExamTypesQuery(PublicExamLookupKind Kind)
    : IRequest<IReadOnlyList<PublicExamTypeOption>>;

public sealed record LookupPublicExamDataQuery(
    int ExamTypeId, PublicExamLookupKind Kind, string StudentCode, DateOnly DateOfBirth)
    : IRequest<PublicExamLookupData?>;

public sealed class ListPublicExamTypesHandler(IPublicExamLookupService service)
    : IRequestHandler<ListPublicExamTypesQuery, IReadOnlyList<PublicExamTypeOption>>
{
    public Task<IReadOnlyList<PublicExamTypeOption>> Handle(ListPublicExamTypesQuery request, CancellationToken cancellationToken) =>
        service.ListTypesAsync(request.Kind, cancellationToken);
}

public sealed class LookupPublicExamDataHandler(IPublicExamLookupService service)
    : IRequestHandler<LookupPublicExamDataQuery, PublicExamLookupData?>
{
    public Task<PublicExamLookupData?> Handle(LookupPublicExamDataQuery request, CancellationToken cancellationToken) =>
        service.LookupAsync(request.ExamTypeId, request.Kind, request.StudentCode, request.DateOfBirth, cancellationToken);
}
