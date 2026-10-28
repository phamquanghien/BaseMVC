namespace BaseMVC.Application.Roles;

public sealed record RoleListItem(string Id, string Name, int UserCount);
public sealed record RoleSearchResponse(IReadOnlyList<RoleListItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record RoleOperationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static RoleOperationResult Success(object? value = null) => new(200, value);
    public static RoleOperationResult Created(object value) => new(201, value);
    public static RoleOperationResult NoContent() => new(204);
    public static RoleOperationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static RoleOperationResult NotFound(string error) => new(404, Errors: [error]);
    public static RoleOperationResult Conflict(string error) => new(409, Errors: [error]);
}
