using System.Security.Claims;
using BaseMVC.Application.Faculties;
using BaseMVC.WebUI.Models.Faculties;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Faculties")]
public sealed class FacultiesController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.Faculties.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Faculties/Index.cshtml");

    [Authorize(Policy = "Permissions.Faculties.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<FacultySearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchFacultiesQuery(search, page, pageSize), cancellationToken));

    [Authorize(Policy = "Permissions.Faculties.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] FacultyMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateFacultyCommand(request.FacultyCode, request.FacultyName, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.FacultyCode.Trim() });
    }

    [Authorize(Policy = "Permissions.Faculties.Update")]
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] FacultyMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new EditFacultyCommand(id, request.FacultyCode, request.FacultyName, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.Faculties.Delete")]
    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteFacultyCommand(id, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(FacultyOperationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
