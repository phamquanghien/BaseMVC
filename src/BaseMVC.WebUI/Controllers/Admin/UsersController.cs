using BaseMVC.Application.Users;
using BaseMVC.WebUI.Models.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Users")]
public sealed class UsersController(ISender sender, IAuthorizationService authorizationService) : Controller
{
    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Users/Index.cshtml");

    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<UserSearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchUsersQuery(search, page, pageSize, User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value), cancellationToken));

    [Authorize(Policy = "Permissions.Users.Read")]
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<AssignableRole>>> GetAssignableRoles(CancellationToken cancellationToken)
    {
        var canManage = (await authorizationService.AuthorizeAsync(User, resource: null, "Permissions.Access.Manage")).Succeeded;
        return Ok(await sender.Send(new GetAssignableRolesQuery(canManage), cancellationToken));
    }

    [Authorize(Policy = "Permissions.Users.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest request, CancellationToken cancellationToken)
    {
        var actor = CurrentUserId();
        var canManage = (await authorizationService.AuthorizeAsync(User, resource: null, "Permissions.Access.Manage")).Succeeded;
        var result = await sender.Send(new CreateUserCommand(request.Email, request.Password, request.RoleIds, actor, canManage), cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.Email.Trim() });
    }

    [Authorize(Policy = "Permissions.Users.Update")]
    [HttpPost("{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var actor = CurrentUserId();
        var canManage = (await authorizationService.AuthorizeAsync(User, resource: null, "Permissions.Access.Manage")).Succeeded;
        var result = await sender.Send(new EditUserCommand(id, request.Email, request.RoleIds, request.IsLocked, actor, canManage), cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.Users.Delete")]
    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteUserCommand(id, CurrentUserId()), cancellationToken);
        return ToActionResult(result);
    }

    private string? CurrentUserId() => User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private IActionResult ToActionResult(UserMutationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        403 => StatusCode(403, new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
