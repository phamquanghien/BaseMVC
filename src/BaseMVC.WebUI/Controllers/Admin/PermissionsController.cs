using System.Security.Claims;
using BaseMVC.Application.Permissions;
using BaseMVC.WebUI.Models.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Permissions")]
public sealed class PermissionsController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Permissions/Index.cshtml");

    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPermissionRolesQuery(), cancellationToken));

    [Authorize(Policy = "Permissions.Access.Read")]
    [HttpGet("roles/{roleId}")]
    public async Task<ActionResult<RolePermissions>> GetRolePermissions(string roleId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRolePermissionsQuery(roleId), cancellationToken);
        return result is null ? NotFound(new { errors = new[] { "Không tìm thấy role." } }) : Ok(result);
    }

    [Authorize(Policy = "Permissions.Access.Manage")]
    [HttpPost("roles/{roleId}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<RolePermissions>> UpdateRolePermissions(
        string roleId, [FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateRolePermissionsCommand(
            roleId, request.Permissions, User.FindFirstValue(ClaimTypes.NameIdentifier)), cancellationToken);
        return result.StatusCode switch
        {
            200 => Ok(result.Value),
            400 => BadRequest(new { errors = result.Errors }),
            404 => NotFound(new { errors = result.Errors }),
            409 => Conflict(new { errors = result.Errors }),
            _ => StatusCode(result.StatusCode, result.Value)
        };
    }
}
