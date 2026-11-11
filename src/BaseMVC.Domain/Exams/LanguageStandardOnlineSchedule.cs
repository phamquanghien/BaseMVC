namespace BaseMVC.Domain.Exams;

/// <summary>Lịch thi chuẩn đầu ra Ngoại ngữ trực tuyến, tách riêng với lịch thi tập trung.</summary>
public sealed class LanguageStandardOnlineSchedule : StudentExamRecord
{
    public const int ShortTextMaxLength = 200;
    public const int NoteMaxLength = 1000;

    public string? RetainedSection { get; set; }

    public string? RetentionBatch { get; set; }

    public string? Note { get; set; }

    public string? ExamDate { get; set; }

    public string? ExamRoom { get; set; }

    public string? TeamsLoginStartTime { get; set; }

    public string? EExamsStartTime { get; set; }

    public string? ExamDateSpeaking { get; set; }

    public string? ExamRoomSpeaking { get; set; }

    public string? TeamsLoginStartTimeSpeaking { get; set; }
}
