using System.ComponentModel.DataAnnotations;

namespace BaseMVC.WebUI.Models.Permissions;

public sealed class UpdateRolePermissionsRequest
{
    [Required]
    public List<string>? Permissions { get; set; } = [];
}
