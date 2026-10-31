using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.ExamTypes;

public sealed class ExamTypeMutationRequest
{
    [Required(ErrorMessage = "Tên loại kỳ thi là bắt buộc.")]
    [StringLength(500, ErrorMessage = "Tên loại kỳ thi không được vượt quá 500 ký tự.")]
    public string Name { get; set; } = string.Empty;
}
