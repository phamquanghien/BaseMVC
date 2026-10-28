namespace BaseMVC.Application.Auditing;

public sealed record AuditLogSearchResponse(
    IReadOnlyList<AuditLogListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
