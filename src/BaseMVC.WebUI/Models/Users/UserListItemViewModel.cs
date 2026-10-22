namespace BaseMVC.WebUI.Models.Users;

public sealed record UserListItemViewModel(
    string Id,
    string Email,
    bool IsLocked,
    bool IsCurrentUser,
    IReadOnlyList<string> RoleNames);
