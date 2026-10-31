using System.Security.Claims;
using BaseMVC.Application.Exams;
using BaseMVC.WebUI.Models.Exams;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Exams")]
public sealed class ExamsController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.Exams.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Exams/Index.cshtml");

    [Authorize(Policy = "Permissions.Exams.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<ExamSearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int? examTypeId, [FromQuery] string? status,
        [FromQuery] bool includeDeleted = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchExamsQuery(search, examTypeId, status, includeDeleted, page, pageSize), cancellationToken));

    [Authorize(Policy = "Permissions.Exams.Read")]
    [HttpGet("exam-types")]
    public async Task<ActionResult<IReadOnlyList<ExamTypeOption>>> ExamTypes(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListExamTypeOptionsQuery(), cancellationToken));

    [Authorize(Policy = "Permissions.Exams.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ExamMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateExamCommand(request.Name, request.ExamTypeId, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.Name.Trim() });
    }

    [Authorize(Policy = "Permissions.Exams.Update")]
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] ExamMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new EditExamCommand(id, request.Name, request.ExamTypeId, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.Exams.Delete")]
    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteExamCommand(id, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(ExamOperationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
