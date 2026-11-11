namespace BaseMVC.WebUI.Models.Home;

public sealed class PublicExamLookupRequest
{
    public int ExamTypeId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public string DateOfBirth { get; set; } = string.Empty;

    public string RecaptchaToken { get; set; } = string.Empty;
}
