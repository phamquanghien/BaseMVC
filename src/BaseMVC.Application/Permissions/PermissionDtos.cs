namespace BaseMVC.Application.Permissions;

public sealed record PermissionRole(string Id, string Name);
public sealed record PermissionItem(string Name, string Module, string Action, bool IsGranted);
public sealed record RolePermissions(string RoleId, string RoleName, IReadOnlyList<PermissionItem> Permissions);
public sealed record PermissionUpdateResult(int StatusCode, object? Value = null, IReadOnlyList<string>? Errors = null)
{
    public static PermissionUpdateResult Success(object value) => new(200, value);
    public static PermissionUpdateResult BadRequest(string error) => new(400, Errors: [error]);
    public static PermissionUpdateResult NotFound(string error) => new(404, Errors: [error]);
    public static PermissionUpdateResult Conflict(string error) => new(409, Errors: [error]);
}
