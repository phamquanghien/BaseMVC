namespace BaseMVC.WebUI.Models.Audit;

public sealed record AuditLogListItem(
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string Outcome,
    string? ActorUserId,
    string? TargetUserId,
    string? TargetRoleId);
