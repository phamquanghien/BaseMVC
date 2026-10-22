namespace BaseMVC.Infrastructure.Auditing;

public interface IAuditLogWriter
{
    Task WriteAsync(
        AuditEventType eventType,
        AuditOutcome outcome,
        string? actorUserId = null,
        string? targetUserId = null,
        string? targetRoleId = null,
        CancellationToken cancellationToken = default);
}
