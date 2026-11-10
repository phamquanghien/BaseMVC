namespace BaseMVC.Domain.Exams;

/// <summary>Lịch thi thử chuẩn đầu ra CNTT trực tuyến, lưu riêng theo kỳ thi và sinh viên.</summary>
public sealed class OnlineExamSchedule : StudentExamRecord
{
    public const int ShortTextMaxLength = 200;
    public const int NoteMaxLength = 1000;

    public string? MicrosoftTeamsRoomName { get; set; }

    public DateOnly? MockExamDate { get; set; }

    public string? TeamsDeviceLoginStartTime { get; set; }

    public string? EExamsDeviceLoginStartTime { get; set; }

    public string? Note { get; set; }

    public string? RetainedSection { get; set; }
}
