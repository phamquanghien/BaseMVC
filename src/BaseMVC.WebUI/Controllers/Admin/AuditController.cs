using BaseMVC.Application.Auditing;
using BaseMVC.Infrastructure.Auditing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Audit")]
[Authorize(Policy = "Permissions.Audit.Read")]
public sealed class AuditController(ISender sender) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Admin/Audit/Index.cshtml");

    [HttpGet("search")]
    public async Task<ActionResult<AuditLogSearchResponse>> Search(
        [FromQuery] string? search,
        [FromQuery] string? eventType,
        [FromQuery] string? outcome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (search?.Length > 100)
        {
            return BadRequest(new { errors = new[] { "Từ khóa tìm kiếm tối đa 100 ký tự." } });
        }

        if (!string.IsNullOrEmpty(eventType)
            && !Enum.GetNames<AuditEventType>().Contains(eventType, StringComparer.Ordinal))
        {
            return BadRequest(new { errors = new[] { "Loại sự kiện không hợp lệ." } });
        }

        if (!string.IsNullOrEmpty(outcome)
            && !Enum.GetNames<AuditOutcome>().Contains(outcome, StringComparer.Ordinal))
        {
            return BadRequest(new { errors = new[] { "Kết quả sự kiện không hợp lệ." } });
        }

        var result = await sender.Send(
            new SearchAuditLogsQuery(search, eventType, outcome, page, pageSize),
            cancellationToken);
        return Ok(result);
    }
}
