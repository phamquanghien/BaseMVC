using BaseMVC.Application.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Auditing;

public sealed class AuditLogReadStore(ApplicationDbContext dbContext) : IAuditLogReadStore
{
    public async Task<AuditLogSearchResponse> SearchAsync(
        string? search,
        string? eventType,
        string? outcome,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var entries = dbContext.AuditLogEntries.AsNoTracking();
        if (!string.IsNullOrEmpty(eventType))
        {
            entries = entries.Where(entry => entry.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(outcome))
        {
            entries = entries.Where(entry => entry.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            entries = entries.Where(entry =>
                entry.EventType.Contains(searchTerm)
                || entry.Outcome.Contains(searchTerm)
                || (entry.ActorUserId != null && entry.ActorUserId.Contains(searchTerm))
                || (entry.TargetUserId != null && entry.TargetUserId.Contains(searchTerm))
                || (entry.TargetRoleId != null && entry.TargetRoleId.Contains(searchTerm)));
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = await entries
            .OrderByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AuditLogListItem(
                entry.OccurredAtUtc,
                entry.EventType,
                entry.Outcome,
                entry.ActorUserId,
                entry.TargetUserId,
                entry.TargetRoleId))
            .ToListAsync(cancellationToken);

        return new AuditLogSearchResponse(items, page, pageSize, totalCount, totalPages);
    }
}
