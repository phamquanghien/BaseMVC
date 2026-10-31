namespace BaseMVC.Application.ExamTypes;

public sealed record ExamTypeListItem(
    int Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record ExamTypeSearchResponse(
    IReadOnlyList<ExamTypeListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ExamTypeOperationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static ExamTypeOperationResult Success(object? value = null) => new(200, value);
    public static ExamTypeOperationResult Created(object value) => new(201, value);
    public static ExamTypeOperationResult NoContent() => new(204);
    public static ExamTypeOperationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static ExamTypeOperationResult NotFound(string error) => new(404, Errors: [error]);
    public static ExamTypeOperationResult Conflict(string error) => new(409, Errors: [error]);
}
