using BaseMVC.Domain.Common;
using BaseMVC.Domain.Exams;

namespace BaseMVC.Domain.ExamTypes;

public sealed class ExamType : BaseAuditableEntity<int>
{
    public const int NameMaxLength = 500;

    public string Name { get; set; } = string.Empty;

    /// <summary>Định dạng dữ liệu thi của các kỳ thi thuộc loại này; null nghĩa là chưa gán nên chưa nhập Excel được.</summary>
    public ExamDataFormat? DataFormat { get; set; }
}
