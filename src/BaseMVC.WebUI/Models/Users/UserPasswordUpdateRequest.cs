using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Users;

public sealed class UserPasswordUpdateRequest
{
    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(12, ErrorMessage = "Mật khẩu phải có ít nhất 12 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
