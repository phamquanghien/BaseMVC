using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Students;

public sealed class StudentMutationRequest
{
    [Required(ErrorMessage = "Mã sinh viên là bắt buộc.")]
    [StringLength(12, ErrorMessage = "Mã sinh viên không được vượt quá 12 ký tự.")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Họ không được vượt quá 100 ký tự.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã lớp là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Mã lớp không được vượt quá 200 ký tự.")]
    public string ClassCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã khoa là bắt buộc.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Mã khoa phải gồm đúng 2 ký tự.")]
    public string FacultyCode { get; set; } = string.Empty;

    public bool Gender { get; set; }

    public bool EligibleForEnglish { get; set; }

    public bool PassedLanguageRequirement { get; set; }

    public bool PassedITRequirement { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
