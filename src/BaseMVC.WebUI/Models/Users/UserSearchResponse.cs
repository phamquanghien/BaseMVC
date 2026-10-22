namespace BaseMVC.WebUI.Models.Users;

public sealed record UserSearchResponse(
    IReadOnlyList<UserListItemViewModel> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
