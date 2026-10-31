namespace BaseMVC.Application.Exams;

/// <summary>Định dạng file Excel. Mỗi định dạng có bảng dữ liệu riêng; thêm giá trị mới khi bổ sung loại kỳ thi.</summary>
public enum ExamImportFormat
{
    ItStandard
}

public enum ExamImportKind
{
    Candidates,
    Results
}

public enum ExamImportMode
{
    /// <summary>Xóa dữ liệu cũ của kỳ thi rồi lưu dữ liệu từ file.</summary>
    ReplaceAll,

    /// <summary>Giữ dữ liệu cũ, bỏ qua thí sinh đã có trong kỳ thi.</summary>
    AddOnly
}

public sealed record ExamImportRowError(int Row, string Message);

public sealed record ExamImportResult(int TotalRows, int ImportedCount, IReadOnlyList<ExamImportRowError> Errors);

public sealed record ExamImportOutcome(int StatusCode, ExamImportResult? Result = null, string? Error = null)
{
    public static ExamImportOutcome Success(ExamImportResult result) => new(200, result);
    public static ExamImportOutcome BadRequest(string error) => new(400, Error: error);
    public static ExamImportOutcome NotFound(string error) => new(404, Error: error);
    public static ExamImportOutcome Conflict(string error) => new(409, Error: error);
}

public sealed record ExamSummaryDto(
    int Id,
    string Name,
    int ExamTypeId,
    string ExamTypeName,
    string Status,
    int ItCandidateCount,
    int ItResultCount);

public sealed record ItExamCandidateDto(
    int Id,
    string StudentCode,
    string LastName,
    string FirstName,
    string? ExamDate,
    string? Session,
    string? ExamDuration,
    string? ExamRoom,
    string? RetentionNote,
    string? RetainedSection);

public sealed record ItExamResultDto(
    int Id,
    string StudentCode,
    string LastName,
    string FirstName,
    string? MultipleChoiceScore,
    string? PracticalScore,
    string? FinalScore,
    string? Note);

public sealed record ExamPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
