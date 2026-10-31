using System.Security.Claims;
using BaseMVC.Application.ExamTypes;
using BaseMVC.WebUI.Models.ExamTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/ExamTypes")]
public sealed class ExamTypesController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.ExamTypes.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/ExamTypes/Index.cshtml");

    [Authorize(Policy = "Permissions.ExamTypes.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<ExamTypeSearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchExamTypesQuery(search, page, pageSize), cancellationToken));

    [Authorize(Policy = "Permissions.ExamTypes.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ExamTypeMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateExamTypeCommand(request.Name, request.DataFormat, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.Name.Trim() });
    }

    [Authorize(Policy = "Permissions.ExamTypes.Update")]
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] ExamTypeMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new EditExamTypeCommand(id, request.Name, request.DataFormat, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.ExamTypes.Delete")]
    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteExamTypeCommand(id, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(ExamTypeOperationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
