using MediatR;

namespace BaseMVC.Application.Auditing;

public sealed record SearchAuditLogsQuery(
    string? Search,
    string? EventType,
    string? Outcome,
    int Page,
    int PageSize) : IRequest<AuditLogSearchResponse>;
