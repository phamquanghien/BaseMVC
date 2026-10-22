namespace BaseMVC.WebUI.Models.Roles;

public sealed record RoleSearchResponse(
    IReadOnlyList<RoleListItemViewModel> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
