namespace BaseMVC.Domain.Exams;

/// <summary>Danh sách thi của kỳ thi Chuẩn đầu ra CNTT.</summary>
public sealed class ItExamCandidate : StudentExamRecord
{
    public const int ShortTextMaxLength = 100;
    public const int NoteMaxLength = 500;

    public DateOnly? ExamDate { get; set; }

    public string? Session { get; set; }

    public string? ExamDuration { get; set; }

    public string? ExamRoom { get; set; }

    public string? RetentionNote { get; set; }

    public string? RetainedSection { get; set; }
}
