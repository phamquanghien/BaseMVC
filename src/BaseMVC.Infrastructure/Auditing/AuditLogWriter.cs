using BaseMVC.Infrastructure.Persistence;

namespace BaseMVC.Infrastructure.Auditing;

public sealed class AuditLogWriter(ApplicationDbContext dbContext) : IAuditLogWriter
{
    public async Task WriteAsync(
        AuditEventType eventType,
        AuditOutcome outcome,
        string? actorUserId = null,
        string? targetUserId = null,
        string? targetRoleId = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            EventType = eventType.ToString(),
            Outcome = outcome.ToString(),
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            TargetRoleId = targetRoleId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
