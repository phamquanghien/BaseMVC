using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Exams;

public sealed class ExamMutationRequest
{
    [Required(ErrorMessage = "Tên kỳ thi là bắt buộc.")]
    [StringLength(500, ErrorMessage = "Tên kỳ thi không được vượt quá 500 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại kỳ thi.")]
    public int ExamTypeId { get; set; }
}
