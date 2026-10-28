using MediatR;

namespace BaseMVC.Application.Auditing;

public sealed class SearchAuditLogsQueryHandler(IAuditLogReadStore readStore)
    : IRequestHandler<SearchAuditLogsQuery, AuditLogSearchResponse>
{
    public Task<AuditLogSearchResponse> Handle(
        SearchAuditLogsQuery request,
        CancellationToken cancellationToken) =>
        readStore.SearchAsync(
            request.Search,
            request.EventType,
            request.Outcome,
            request.Page,
            request.PageSize,
            cancellationToken);
}
