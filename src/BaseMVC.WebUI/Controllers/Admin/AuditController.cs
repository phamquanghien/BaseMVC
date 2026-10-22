using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.WebUI.Controllers.Admin;

[ApiController]
[Route("Admin/Audit")]
[Authorize(Policy = "Permissions.Audit.Read")]
public sealed class AuditController(ApplicationDbContext dbContext) : Controller
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var entries = dbContext.AuditLogEntries.AsNoTracking();
        if (!string.IsNullOrEmpty(eventType))
        {
            entries = entries.Where(entry => entry.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(outcome))
        {
            entries = entries.Where(entry => entry.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            entries = entries.Where(entry =>
                entry.EventType.Contains(searchTerm)
                || entry.Outcome.Contains(searchTerm)
                || (entry.ActorUserId != null && entry.ActorUserId.Contains(searchTerm))
                || (entry.TargetUserId != null && entry.TargetUserId.Contains(searchTerm))
                || (entry.TargetRoleId != null && entry.TargetRoleId.Contains(searchTerm)));
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = await entries
            .OrderByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AuditLogListItem(
                entry.OccurredAtUtc,
                entry.EventType,
                entry.Outcome,
                entry.ActorUserId,
                entry.TargetUserId,
                entry.TargetRoleId))
            .ToListAsync(cancellationToken);

        return Ok(new AuditLogSearchResponse(items, page, pageSize, totalCount, totalPages));
    }
}
