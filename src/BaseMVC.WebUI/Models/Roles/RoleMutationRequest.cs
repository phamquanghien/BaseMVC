using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Roles;

public sealed class RoleMutationRequest
{
    [Required(ErrorMessage = "Tên role là bắt buộc.")]
    [StringLength(128, ErrorMessage = "Tên role không được vượt quá 128 ký tự.")]
    public string Name { get; set; } = string.Empty;
}
