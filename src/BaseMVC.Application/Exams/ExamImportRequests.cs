using MediatR;

namespace BaseMVC.Application.Exams;

public sealed record GetExamImportTemplateQuery(ExamImportFormat Format, ExamImportKind Kind) : IRequest<byte[]>;

public sealed record ImportExamDataCommand(
    int ExamId, ExamImportFormat Format, ExamImportKind Kind, ExamImportMode Mode,
    Stream Content, string? ActorUserId) : IRequest<ExamImportOutcome>;

public sealed record GetExamSummaryQuery(int ExamId) : IRequest<ExamSummaryDto?>;

public sealed record SearchItExamCandidatesQuery(int ExamId, string? Search, int Page, int PageSize)
    : IRequest<ExamPagedResponse<ItExamCandidateDto>?>;

public sealed record SearchItExamResultsQuery(int ExamId, string? Search, int Page, int PageSize)
    : IRequest<ExamPagedResponse<ItExamResultDto>?>;

public sealed class GetExamImportTemplateHandler(IExamImportService service)
    : IRequestHandler<GetExamImportTemplateQuery, byte[]>
{
    public Task<byte[]> Handle(GetExamImportTemplateQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(service.BuildTemplate(request.Format, request.Kind));
}

public sealed class ImportExamDataHandler(IExamImportService service)
    : IRequestHandler<ImportExamDataCommand, ExamImportOutcome>
{
    public Task<ExamImportOutcome> Handle(ImportExamDataCommand request, CancellationToken cancellationToken) =>
        service.ImportAsync(
            request.ExamId, request.Format, request.Kind, request.Mode, request.Content, request.ActorUserId, cancellationToken);
}

public sealed class GetExamSummaryHandler(IExamImportService service)
    : IRequestHandler<GetExamSummaryQuery, ExamSummaryDto?>
{
    public Task<ExamSummaryDto?> Handle(GetExamSummaryQuery request, CancellationToken cancellationToken) =>
        service.GetSummaryAsync(request.ExamId, cancellationToken);
}

public sealed class SearchItExamCandidatesHandler(IExamImportService service)
    : IRequestHandler<SearchItExamCandidatesQuery, ExamPagedResponse<ItExamCandidateDto>?>
{
    public Task<ExamPagedResponse<ItExamCandidateDto>?> Handle(SearchItExamCandidatesQuery request, CancellationToken cancellationToken) =>
        service.SearchItCandidatesAsync(request.ExamId, request.Search, request.Page, request.PageSize, cancellationToken);
}

public sealed class SearchItExamResultsHandler(IExamImportService service)
    : IRequestHandler<SearchItExamResultsQuery, ExamPagedResponse<ItExamResultDto>?>
{
    public Task<ExamPagedResponse<ItExamResultDto>?> Handle(SearchItExamResultsQuery request, CancellationToken cancellationToken) =>
        service.SearchItResultsAsync(request.ExamId, request.Search, request.Page, request.PageSize, cancellationToken);
}
