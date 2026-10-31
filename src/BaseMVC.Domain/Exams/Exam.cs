using BaseMVC.Domain.Common;
using BaseMVC.Domain.ExamTypes;

namespace BaseMVC.Domain.Exams;

public sealed class Exam : BaseAuditableEntity<int>
{
    public const int NameMaxLength = 500;
    public const int StatusMaxLength = 32;

    public string Name { get; set; } = string.Empty;

    public int ExamTypeId { get; set; }

    public ExamType? ExamType { get; set; }

    public ExamStatus Status { get; set; } = ExamStatus.NotStarted;

    public bool IsDeleted { get; set; }
}
