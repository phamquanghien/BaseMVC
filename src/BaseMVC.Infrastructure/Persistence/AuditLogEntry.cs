namespace BaseMVC.Infrastructure.Persistence;

public sealed class AuditLogEntry
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string EventType { get; set; }

    public required string Outcome { get; set; }

    public string? ActorUserId { get; set; }

    public string? TargetUserId { get; set; }

    public string? TargetRoleId { get; set; }
}
