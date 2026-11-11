namespace BaseMVC.Domain.Exams;

/// <summary>Lịch thi chuẩn đầu ra CNTT trực tuyến; tách riêng với lịch thi tập trung.</summary>
public sealed class ItStandardOnlineSchedule : StudentExamRecord
{
    public const int ShortTextMaxLength = 200;
    public const int NoteMaxLength = 1000;

    public string? Session { get; set; }

    public string? TeamsLoginStartTime { get; set; }

    public string? EExamsLoginStartTime { get; set; }

    public string? MicrosoftTeamsRoomName { get; set; }

    public string? Note { get; set; }

    public string? RetainedSection { get; set; }
}
