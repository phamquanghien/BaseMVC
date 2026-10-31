namespace BaseMVC.Application.Exams;

public sealed record ExamListItem(
    int Id,
    string Name,
    int ExamTypeId,
    string ExamTypeName,
    string Status,
    bool IsDeleted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record ExamSearchResponse(
    IReadOnlyList<ExamListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ExamTypeOption(int Id, string Name);

public sealed record ExamOperationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static ExamOperationResult Success(object? value = null) => new(200, value);
    public static ExamOperationResult Created(object value) => new(201, value);
    public static ExamOperationResult NoContent() => new(204);
    public static ExamOperationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static ExamOperationResult NotFound(string error) => new(404, Errors: [error]);
    public static ExamOperationResult Conflict(string error) => new(409, Errors: [error]);
}
