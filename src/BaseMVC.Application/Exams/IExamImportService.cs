namespace BaseMVC.Application.Exams;

public interface IExamImportService
{
    byte[] BuildTemplate(ExamImportFormat format, ExamImportKind kind);

    Task<ExamImportOutcome> ImportAsync(
        int examId, ExamImportFormat format, ExamImportKind kind, ExamImportMode mode,
        Stream content, string? actorUserId, CancellationToken cancellationToken);

    Task<ExamSummaryDto?> GetSummaryAsync(int examId, CancellationToken cancellationToken);

    Task<ExamPagedResponse<ItExamCandidateDto>?> SearchItCandidatesAsync(
        int examId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<ExamPagedResponse<ItExamResultDto>?> SearchItResultsAsync(
        int examId, string? search, int page, int pageSize, CancellationToken cancellationToken);
}
