namespace BaseMVC.Domain.Common;

public abstract class BaseAuditableEntity<T> : BaseEntity<T>
{
    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedAtUtc { get; set; }
}
