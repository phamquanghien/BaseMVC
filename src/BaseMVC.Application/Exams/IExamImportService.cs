namespace BaseMVC.Application.Exams;

public interface IExamImportService
{
    /// <summary>Mẫu Excel theo định dạng của loại kỳ thi mà kỳ thi thuộc về.</summary>
    Task<ExamDataOutcome<byte[]>> BuildTemplateAsync(int examId, ExamImportKind kind, CancellationToken cancellationToken);

    Task<ExamImportOutcome> ImportAsync(
        int examId, ExamImportKind kind, ExamImportMode mode,
        Stream content, string? actorUserId, CancellationToken cancellationToken);

    Task<ExamSummaryDto?> GetSummaryAsync(int examId, CancellationToken cancellationToken);

    Task<ExamDataOutcome<ExamDataPage>> SearchDataAsync(
        int examId, ExamImportKind kind, string? search, int page, int pageSize,
        CancellationToken cancellationToken);
}
