using System.Security.Claims;
using BaseMVC.Application.Students;
using BaseMVC.WebUI.Models.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Students")]
public sealed class StudentsController(ISender sender) : Controller
{
    [Authorize(Policy = "Permissions.Students.Read")]
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Students/Index.cshtml");

    [Authorize(Policy = "Permissions.Students.Read")]
    [HttpGet("search")]
    public async Task<ActionResult<StudentSearchResponse>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchStudentsQuery(search, page, pageSize), cancellationToken));

    [Authorize(Policy = "Permissions.Students.Read")]
    [HttpGet("faculty-options")]
    public async Task<ActionResult<IReadOnlyList<FacultyOption>>> FacultyOptions(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListFacultyOptionsQuery(), cancellationToken));

    [Authorize(Policy = "Permissions.Students.Create")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] StudentMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateStudentCommand(ToInput(request), User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result, nameof(Search), new { search = request.StudentCode.Trim() });
    }

    [Authorize(Policy = "Permissions.Students.Update")]
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromBody] StudentMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new EditStudentCommand(id, ToInput(request), User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Permissions.Students.Delete")]
    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteStudentCommand(id, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return ToActionResult(result);
    }

    private static StudentInput ToInput(StudentMutationRequest request) => new(
        request.StudentCode, request.LastName, request.FirstName, request.ClassCode, request.FacultyCode,
        request.Gender, request.EligibleForEnglish, request.PassedLanguageRequirement,
        request.PassedITRequirement, request.DateOfBirth);

    private IActionResult ToActionResult(StudentOperationResult result, string? action = null, object? routeValues = null) => result.StatusCode switch
    {
        201 => CreatedAtAction(action!, routeValues, result.Value),
        204 => NoContent(),
        400 => BadRequest(new { errors = result.Errors }),
        404 => NotFound(new { errors = result.Errors }),
        409 => Conflict(new { errors = result.Errors }),
        _ => Ok(result.Value)
    };
}
