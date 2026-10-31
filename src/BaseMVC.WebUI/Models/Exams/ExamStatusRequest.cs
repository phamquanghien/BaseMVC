using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Exams;

public sealed class ExamStatusRequest
{
    [Required(ErrorMessage = "Vui lòng chọn trạng thái kỳ thi.")]
    public string Status { get; set; } = string.Empty;
}
