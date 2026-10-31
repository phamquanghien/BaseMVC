using BaseMVC.Domain.Common;
using BaseMVC.Domain.Students;

namespace BaseMVC.Domain.Exams;

/// <summary>Kết quả thi của kỳ thi Chuẩn đầu ra CNTT. Điểm lưu dạng chuỗi theo chuẩn của file Excel.</summary>
public sealed class ItExamResult : BaseAuditableEntity<int>
{
    public const int ScoreMaxLength = 50;
    public const int NoteMaxLength = 1000;

    public int ExamId { get; set; }

    public Exam? Exam { get; set; }

    public int StudentId { get; set; }

    /// <summary>Họ tên và mã sinh viên luôn lấy từ bảng Student để không bị lệch dữ liệu.</summary>
    public Student? Student { get; set; }

    public string? MultipleChoiceScore { get; set; }

    public string? PracticalScore { get; set; }

    public string? FinalScore { get; set; }

    public string? Note { get; set; }
}
