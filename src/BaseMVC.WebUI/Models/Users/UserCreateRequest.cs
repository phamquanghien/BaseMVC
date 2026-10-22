using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Users;

public sealed class UserCreateRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu ban đầu là bắt buộc.")]
    [MinLength(12, ErrorMessage = "Mật khẩu phải có ít nhất 12 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public List<string> RoleIds { get; set; } = [];
}
