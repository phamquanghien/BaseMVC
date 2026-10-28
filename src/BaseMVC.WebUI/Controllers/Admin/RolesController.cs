using System.Security.Claims;
using BaseMVC.Application.Roles;
using BaseMVC.WebUI.Models.Roles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Roles")]
public sealed class RolesController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.Roles.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Roles/Index.cshtml");

    [Authorize(Policy = "Permissions.Roles.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<RoleSearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchRolesQuery(search, page, pageSize), cancellationToken));

    [Authorize(Policy = "Permissions.Roles.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] RoleMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateRoleCommand(request.Name, User.FindFirstValue(ClaimTypes.NameIdentifier)), cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.Name.Trim() });
    }

    [Authorize(Policy = "Permissions.Roles.Update")]
    [HttpPost("{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] RoleMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EditRoleCommand(id, request.Name, User.FindFirstValue(ClaimTypes.NameIdentifier)), cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.Roles.Delete")]
    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteRoleCommand(id, User.FindFirstValue(ClaimTypes.NameIdentifier)), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(RoleOperationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
