namespace BaseMVC.WebUI.Models.Permissions;

public sealed record RolePermissionsViewModel(
    string RoleId,
    string RoleName,
    IReadOnlyList<PermissionViewModel> Permissions);

public sealed record PermissionViewModel(
    string Name,
    string Module,
    string Action,
    bool IsGranted);
