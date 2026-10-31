namespace BaseMVC.Domain.Exams;

/// <summary>Danh sách thi Chuẩn đầu ra Ngoại ngữ.</summary>
public sealed class LanguageStandardCandidate : StudentExamRecord
{
    public string? RetentionExam { get; set; }

    public string? RetainedSection { get; set; }

    public string? Note { get; set; }

    public DateOnly? ExamDate { get; set; }

    public string? Session { get; set; }

    public string? ExamRoom { get; set; }

    public DateOnly? ExamDateSpeaking { get; set; }

    public string? SessionSpeaking { get; set; }

    public string? ExamRoomSpeaking { get; set; }
}

/// <summary>Kết quả thi Chuẩn đầu ra Ngoại ngữ. Điểm lưu dạng chuỗi theo file Excel.</summary>
public sealed class LanguageStandardResult : StudentExamRecord
{
    public string? Listening { get; set; }

    public string? Reading { get; set; }

    public string? Writing { get; set; }

    public string? Speaking { get; set; }

    public string? FinalScore { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }
}

/// <summary>Danh sách thi Kiểm tra trình độ Ngoại ngữ.</summary>
public sealed class LanguagePlacementCandidate : StudentExamRecord
{
    public string? Note { get; set; }

    public DateOnly? ExamDate { get; set; }

    public string? Session { get; set; }

    public string? ExamRoom { get; set; }

    public DateOnly? ExamDateSpeaking { get; set; }

    public string? SessionSpeaking { get; set; }

    public string? ExamRoomSpeaking { get; set; }
}

/// <summary>Kết quả thi Kiểm tra trình độ Ngoại ngữ.</summary>
public sealed class LanguagePlacementResult : StudentExamRecord
{
    public string? Listening { get; set; }

    public string? Reading { get; set; }

    public string? Writing { get; set; }

    public string? Speaking { get; set; }

    public string? FinalScore { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }
}

/// <summary>Kết quả thi Tiếng Anh tăng cường (chưa có danh sách thi).</summary>
public sealed class EnglishIntensiveResult : StudentExamRecord
{
    public string? Cohort { get; set; }

    public string? Attendance { get; set; }

    public string? FinalScore { get; set; }

    public string? Group { get; set; }

    public string? Note { get; set; }
}
