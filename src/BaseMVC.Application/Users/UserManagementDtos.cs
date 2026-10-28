namespace BaseMVC.Application.Users;

public sealed record UserListItem(
    string Id, string Email, bool IsLocked, bool IsCurrentUser, IReadOnlyList<string> RoleNames);

public sealed record UserSearchResponse(
    IReadOnlyList<UserListItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record AssignableRole(string Id, string Name, bool IsAssignable);

public sealed record UserMutationResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static UserMutationResult Success(object? value = null) => new(200, value);
    public static UserMutationResult Created(object value) => new(201, value);
    public static UserMutationResult NoContent() => new(204);
    public static UserMutationResult NotFound(string error) => new(404, Errors: [error]);
    public static UserMutationResult BadRequest(IEnumerable<string> errors) => new(400, Errors: errors.ToArray());
    public static UserMutationResult Forbidden(string error) => new(403, Errors: [error]);
    public static UserMutationResult Conflict(string error) => new(409, Errors: [error]);
}
