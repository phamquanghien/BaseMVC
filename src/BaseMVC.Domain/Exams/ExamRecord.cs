using BaseMVC.Domain.Common;
using BaseMVC.Domain.Students;

namespace BaseMVC.Domain.Exams;

/// <summary>Dòng dữ liệu (danh sách thi hoặc kết quả thi) thuộc một kỳ thi.</summary>
public abstract class ExamRecord : BaseAuditableEntity<int>
{
    public int ExamId { get; set; }

    public Exam? Exam { get; set; }
}

/// <summary>Dòng dữ liệu của thí sinh là sinh viên; họ tên và mã sinh viên luôn lấy từ bảng Student.</summary>
public abstract class StudentExamRecord : ExamRecord
{
    public int StudentId { get; set; }

    public Student? Student { get; set; }
}

/// <summary>Độ dài tối đa của các cột chuỗi trong dữ liệu thi, dùng chung cho cấu hình EF và kiểm tra file Excel.</summary>
public static class ExamDataLimits
{
    public static int For(string propertyName) => propertyName switch
    {
        "Note" => 1000,
        "LastName" or "FirstName" => 200,
        "IdentityNumber" or "CandidateNumber" => 50,
        "RetentionExam" or "RetentionNote" or "RetainedSection" => 500,
        "Listening" or "Reading" or "Writing" or "Speaking" or "FinalScore" or "MultipleChoiceScore" or "PracticalScore" => 50,
        _ => 100
    };
}
