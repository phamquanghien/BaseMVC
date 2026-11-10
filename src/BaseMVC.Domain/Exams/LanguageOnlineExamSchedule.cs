namespace BaseMVC.Domain.Exams;

/// <summary>Lịch thi thử chuẩn đầu ra Ngoại ngữ trực tuyến, lưu riêng theo kỳ thi và sinh viên.</summary>
public sealed class LanguageOnlineExamSchedule : StudentExamRecord
{
    public const int ShortTextMaxLength = 200;
    public const int NoteMaxLength = 500;

    public DateOnly? MockExamDate { get; set; }

    public string? MockExamRoom { get; set; }

    public string? TeamsLoginStartTime { get; set; }

    public string? SebLoginStartTime { get; set; }

    public string? RetainedSection { get; set; }

    public string? RetentionBatch { get; set; }
}
