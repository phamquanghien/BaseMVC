namespace BaseMVC.Application.Exams;

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

public sealed record ExamDataOutcome<T>(int StatusCode, T? Value = default, string? Error = null)
{
    public static ExamDataOutcome<T> Success(T value) => new(200, value);
    public static ExamDataOutcome<T> BadRequest(string error) => new(400, Error: error);
    public static ExamDataOutcome<T> NotFound(string error) => new(404, Error: error);
    public static ExamDataOutcome<T> Conflict(string error) => new(409, Error: error);
}

public sealed record ExamDataCount(string Format, string Kind, int Count);

/// <summary>DataFormat null nghĩa là loại kỳ thi chưa được gán định dạng; Counts chỉ gồm định dạng của kỳ thi.</summary>
public sealed record ExamSummaryDto(
    int Id,
    string Name,
    int ExamTypeId,
    string ExamTypeName,
    string Status,
    string? DataFormat,
    bool SupportsCandidates,
    IReadOnlyList<ExamDataCount> Counts);

public sealed record ExamDataColumn(string Key, string Label);

/// <summary>Một trang dữ liệu thi; mỗi dòng là từ điển khóa (camelCase) - giá trị hiển thị.</summary>
public sealed record ExamDataPage(
    IReadOnlyList<ExamDataColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
