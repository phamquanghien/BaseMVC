using BaseMVC.Domain.Common;

namespace BaseMVC.Domain.ExamTypes;

public sealed class ExamType : BaseAuditableEntity<int>
{
    public const int NameMaxLength = 500;

    public string Name { get; set; } = string.Empty;
}
