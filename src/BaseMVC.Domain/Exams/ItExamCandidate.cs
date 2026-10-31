using BaseMVC.Domain.Common;
using BaseMVC.Domain.Students;

namespace BaseMVC.Domain.Exams;

/// <summary>Danh sách thi của kỳ thi Chuẩn đầu ra CNTT.</summary>
public sealed class ItExamCandidate : BaseAuditableEntity<int>
{
    public const int ShortTextMaxLength = 100;
    public const int NoteMaxLength = 500;

    public int ExamId { get; set; }

    public Exam? Exam { get; set; }

    public int StudentId { get; set; }

    /// <summary>Họ tên và mã sinh viên luôn lấy từ bảng Student để không bị lệch dữ liệu.</summary>
    public Student? Student { get; set; }

    public DateOnly? ExamDate { get; set; }

    public string? Session { get; set; }

    public string? ExamDuration { get; set; }

    public string? ExamRoom { get; set; }

    public string? RetentionNote { get; set; }

    public string? RetainedSection { get; set; }
}
