using MediatR;

namespace BaseMVC.Application.Exams;

public sealed record GetExamImportTemplateQuery(int ExamId, ExamImportKind Kind) : IRequest<ExamDataOutcome<byte[]>>;

public sealed record ImportExamDataCommand(
    int ExamId, ExamImportKind Kind, ExamImportMode Mode,
    Stream Content, DateOnly? ValidUntil, string? ActorUserId) : IRequest<ExamImportOutcome>;

public sealed record GetExamSummaryQuery(int ExamId) : IRequest<ExamSummaryDto?>;

public sealed record SearchExamDataQuery(
    int ExamId, ExamImportKind Kind, string? Search, int Page, int PageSize)
    : IRequest<ExamDataOutcome<ExamDataPage>>;

public sealed record UpdateExamScheduleCommand(
    int ExamId, int RecordId, IReadOnlyDictionary<string, string?> Values, string? ActorUserId)
    : IRequest<ExamDataOutcome<bool>>;

public sealed class GetExamImportTemplateHandler(IExamImportService service)
    : IRequestHandler<GetExamImportTemplateQuery, ExamDataOutcome<byte[]>>
{
    public Task<ExamDataOutcome<byte[]>> Handle(GetExamImportTemplateQuery request, CancellationToken cancellationToken) =>
        service.BuildTemplateAsync(request.ExamId, request.Kind, cancellationToken);
}

public sealed class ImportExamDataHandler(IExamImportService service)
    : IRequestHandler<ImportExamDataCommand, ExamImportOutcome>
{
    public Task<ExamImportOutcome> Handle(ImportExamDataCommand request, CancellationToken cancellationToken) =>
        service.ImportAsync(
            request.ExamId, request.Kind, request.Mode, request.Content, request.ValidUntil, request.ActorUserId, cancellationToken);
}

public sealed class GetExamSummaryHandler(IExamImportService service)
    : IRequestHandler<GetExamSummaryQuery, ExamSummaryDto?>
{
    public Task<ExamSummaryDto?> Handle(GetExamSummaryQuery request, CancellationToken cancellationToken) =>
        service.GetSummaryAsync(request.ExamId, cancellationToken);
}

public sealed class SearchExamDataHandler(IExamImportService service)
    : IRequestHandler<SearchExamDataQuery, ExamDataOutcome<ExamDataPage>>
{
    public Task<ExamDataOutcome<ExamDataPage>> Handle(SearchExamDataQuery request, CancellationToken cancellationToken) =>
        service.SearchDataAsync(request.ExamId, request.Kind, request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class UpdateExamScheduleHandler(IExamImportService service)
    : IRequestHandler<UpdateExamScheduleCommand, ExamDataOutcome<bool>>
{
    public Task<ExamDataOutcome<bool>> Handle(UpdateExamScheduleCommand request, CancellationToken cancellationToken) =>
        service.UpdateScheduleAsync(request.ExamId, request.RecordId, request.Values, request.ActorUserId, cancellationToken);
}
