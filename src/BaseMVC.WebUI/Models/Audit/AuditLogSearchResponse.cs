namespace BaseMVC.WebUI.Models.Audit;

public sealed record AuditLogSearchResponse(
    IReadOnlyList<AuditLogListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
