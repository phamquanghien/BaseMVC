namespace BaseMVC.WebUI.Models.Exams;

public sealed class ExamScheduleEditRequest
{
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.Ordinal);
}
