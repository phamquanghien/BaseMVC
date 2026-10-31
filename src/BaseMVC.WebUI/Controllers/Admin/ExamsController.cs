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
    private const int MaxImportFileBytes = 5 * 1024 * 1024;
    private const string UnsupportedMessage = "Loại dữ liệu không hợp lệ.";

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
    [HttpGet("{id:int}")]
    public IActionResult Details(int id) => View("~/Views/Admin/Exams/Details.cshtml", id);

    [Authorize(Policy = "Permissions.Exams.Read")]
    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> Summary(int id, CancellationToken cancellationToken)
    {
        var summary = await sender.Send(new GetExamSummaryQuery(id), cancellationToken);
        return summary is null ? NotFound(new { errors = new[] { "Không tìm thấy kỳ thi." } }) : Ok(summary);
    }

    [Authorize(Policy = "Permissions.Exams.Read")]
    [HttpGet("{id:int}/data")]
    public async Task<IActionResult> Data(
        int id, [FromQuery] ExamImportKind kind, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var outcome = await sender.Send(new SearchExamDataQuery(id, kind, search, page, pageSize), cancellationToken);
        return outcome.StatusCode == 200 ? Ok(outcome.Value) : ToErrorResult(outcome.StatusCode, outcome.Error);
    }

    [Authorize(Policy = "Permissions.Exams.Update")]
    [HttpGet("{id:int}/import/template")]
    public async Task<IActionResult> ImportTemplate(int id, [FromQuery] ExamImportKind kind, CancellationToken cancellationToken)
    {
        var outcome = await sender.Send(new GetExamImportTemplateQuery(id, kind), cancellationToken);
        return outcome.StatusCode == 200
            ? File(
                outcome.Value!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Exam{id}{kind}Template.xlsx")
            : ToErrorResult(outcome.StatusCode, outcome.Error);
    }

    [Authorize(Policy = "Permissions.Exams.Update")]
    [HttpPost("{id:int}/import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportFileBytes)]
    public async Task<IActionResult> Import(
        int id, IFormFile? file, [FromForm] ExamImportKind kind,
        [FromForm] ExamImportMode mode, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
            return BadRequest(new { errors = new[] { "Chế độ nhập không hợp lệ." } });
        if (!Enum.IsDefined(kind))
            return BadRequest(new { errors = new[] { UnsupportedMessage } });
        if (file is null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Vui lòng chọn file Excel (.xlsx)." } });
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { errors = new[] { "Chỉ hỗ trợ file Excel định dạng .xlsx." } });
        if (file.Length > MaxImportFileBytes)
            return BadRequest(new { errors = new[] { "File không được vượt quá 5 MB." } });

        await using var stream = file.OpenReadStream();
        var outcome = await sender.Send(
            new ImportExamDataCommand(id, kind, mode, stream, User.FindFirstValue(ClaimTypes.NameIdentifier)),
            cancellationToken);
        return outcome.StatusCode == 200 ? Ok(outcome.Result) : ToErrorResult(outcome.StatusCode, outcome.Error);
    }

    private IActionResult ToErrorResult(int statusCode, string? error) => statusCode switch
    {
        404 => NotFound(new { errors = new[] { error } }),
        409 => Conflict(new { errors = new[] { error } }),
        _ => BadRequest(new { errors = new[] { error } })
    };

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

    [Authorize(Policy = "Permissions.Exams.ChangeStatus")]
    [HttpPost("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ExamStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ChangeExamStatusCommand(id, request.Status, User.FindFirstValue(ClaimTypes.NameIdentifier)),
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
