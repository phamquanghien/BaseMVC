using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Users;

public sealed class UserUpdateRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public List<string> RoleIds { get; set; } = [];

    public bool IsLocked { get; set; }
}
