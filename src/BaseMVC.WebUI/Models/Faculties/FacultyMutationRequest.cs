using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Faculties;

public sealed class FacultyMutationRequest
{
    [Required(ErrorMessage = "Mã khoa là bắt buộc.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Mã khoa phải gồm đúng 2 ký tự.")]
    public string FacultyCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên khoa là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên khoa không được vượt quá 200 ký tự.")]
    public string FacultyName { get; set; } = string.Empty;
}
