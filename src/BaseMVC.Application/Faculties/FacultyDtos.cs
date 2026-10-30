namespace BaseMVC.Application.Faculties;

public sealed record FacultyListItem(
    int Id,
    string FacultyCode,
    string FacultyName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record FacultySearchResponse(
    IReadOnlyList<FacultyListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record FacultyOperationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static FacultyOperationResult Success(object? value = null) => new(200, value);
    public static FacultyOperationResult Created(object value) => new(201, value);
    public static FacultyOperationResult NoContent() => new(204);
    public static FacultyOperationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static FacultyOperationResult NotFound(string error) => new(404, Errors: [error]);
    public static FacultyOperationResult Conflict(string error) => new(409, Errors: [error]);
}
