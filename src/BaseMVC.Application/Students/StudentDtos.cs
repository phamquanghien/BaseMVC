namespace BaseMVC.Application.Students;

public sealed record StudentInput(
    string StudentCode,
    string LastName,
    string FirstName,
    string ClassCode,
    string FacultyCode,
    bool Gender,
    bool EligibleForEnglish,
    bool PassedLanguageRequirement,
    bool PassedITRequirement,
    DateOnly? DateOfBirth);

public sealed record StudentListItem(
    int Id,
    string StudentCode,
    string LastName,
    string FirstName,
    string ClassCode,
    string FacultyCode,
    string? FacultyName,
    bool Gender,
    bool EligibleForEnglish,
    bool PassedLanguageRequirement,
    bool PassedITRequirement,
    DateOnly? DateOfBirth,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record StudentSearchResponse(
    IReadOnlyList<StudentListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record FacultyOption(string FacultyCode, string FacultyName);

public sealed record StudentOperationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static StudentOperationResult Success(object? value = null) => new(200, value);
    public static StudentOperationResult Created(object value) => new(201, value);
    public static StudentOperationResult NoContent() => new(204);
    public static StudentOperationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static StudentOperationResult NotFound(string error) => new(404, Errors: [error]);
    public static StudentOperationResult Conflict(string error) => new(409, Errors: [error]);
}
