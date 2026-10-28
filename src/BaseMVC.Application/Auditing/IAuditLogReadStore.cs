namespace BaseMVC.Application.Auditing;

public interface IAuditLogReadStore
{
    Task<AuditLogSearchResponse> SearchAsync(
        string? search,
        string? eventType,
        string? outcome,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
