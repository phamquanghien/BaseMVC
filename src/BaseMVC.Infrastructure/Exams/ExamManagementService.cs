using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Exams;

public sealed class ExamManagementService(ApplicationDbContext dbContext, IAuditLogWriter auditLogWriter) : IExamManagementService
{
    private const string ExamNotFoundMessage = "Không tìm thấy kỳ thi.";

    public async Task<ExamSearchResponse> SearchAsync(
        string? search, int? examTypeId, string? status, bool includeDeleted, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        IQueryable<Exam> query = includeDeleted ? dbContext.Exams.IgnoreQueryFilters() : dbContext.Exams;
        query = query.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(exam => exam.Name.ToLower().Contains(term));
        }

        if (examTypeId is not null)
            query = query.Where(exam => exam.ExamTypeId == examTypeId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ExamStatus>(status, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
                return new ExamSearchResponse([], 1, pageSize, 0, 0);
            query = query.Where(exam => exam.Status == parsed);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = await query
            .OrderByDescending(exam => exam.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(exam => new ExamListItem(
                exam.Id, exam.Name, exam.ExamTypeId, exam.ExamType!.Name, exam.Status.ToString(),
                exam.IsDeleted, exam.CreatedAtUtc, exam.LastModifiedAtUtc))
            .ToListAsync(cancellationToken);

        return new ExamSearchResponse(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<IReadOnlyList<ExamTypeOption>> ListExamTypeOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.ExamTypes.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new ExamTypeOption(item.Id, item.Name))
            .ToListAsync(cancellationToken);

    public async Task<ExamOperationResult> CreateAsync(string name, int examTypeId, string? actorUserId, CancellationToken cancellationToken)
    {
        var (normalizedName, errors) = Normalize(name);
        if (errors.Count > 0) return ExamOperationResult.BadRequest(errors);
        if (!await dbContext.ExamTypes.AnyAsync(item => item.Id == examTypeId, cancellationToken))
            return ExamOperationResult.BadRequest(["Loại kỳ thi không tồn tại."]);

        var exam = new Exam
        {
            Name = normalizedName,
            ExamTypeId = examTypeId,
            Status = ExamStatus.NotStarted,
            CreatedBy = actorUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.Exams.Add(exam);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync(AuditEventType.ExamCreated, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);

        return ExamOperationResult.Created(await LoadItemAsync(exam.Id, cancellationToken));
    }

    public async Task<ExamOperationResult> EditAsync(int id, string name, int examTypeId, string? actorUserId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (exam is null) return ExamOperationResult.NotFound(ExamNotFoundMessage);

        var (normalizedName, errors) = Normalize(name);
        if (errors.Count > 0) return ExamOperationResult.BadRequest(errors);

        if (examTypeId != exam.ExamTypeId)
        {
            if (exam.Status != ExamStatus.NotStarted)
                return ExamOperationResult.Conflict("Chỉ được đổi loại kỳ thi khi kỳ thi ở trạng thái “Chưa bắt đầu”.");
            if (!await dbContext.ExamTypes.AnyAsync(item => item.Id == examTypeId, cancellationToken))
                return ExamOperationResult.BadRequest(["Loại kỳ thi không tồn tại."]);
            exam.ExamTypeId = examTypeId;
        }

        exam.Name = normalizedName;
        exam.LastModifiedBy = actorUserId;
        exam.LastModifiedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync(AuditEventType.ExamUpdated, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);

        return ExamOperationResult.Success(await LoadItemAsync(exam.Id, cancellationToken));
    }

    public async Task<ExamOperationResult> ChangeStatusAsync(int id, string status, string? actorUserId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ExamStatus>(status, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            return ExamOperationResult.BadRequest(["Trạng thái kỳ thi không hợp lệ."]);

        var exam = await dbContext.Exams.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (exam is null) return ExamOperationResult.NotFound(ExamNotFoundMessage);

        // Any transition is allowed so a wrong status can always be corrected.
        if (exam.Status != parsed)
        {
            exam.Status = parsed;
            exam.LastModifiedBy = actorUserId;
            exam.LastModifiedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogWriter.WriteAsync(AuditEventType.ExamStatusChanged, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
        }

        return ExamOperationResult.Success(await LoadItemAsync(exam.Id, cancellationToken));
    }

    public async Task<ExamOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (exam is null) return ExamOperationResult.NotFound(ExamNotFoundMessage);

        // Exams that never started and hold no imported data are removed; the rest are kept for history and only hidden.
        var hasData = false;
        foreach (var spec in ExamDataSpecs.Specs)
        {
            if (await spec.CountAsync(dbContext, id, cancellationToken) > 0)
            {
                hasData = true;
                break;
            }
        }

        if (exam.Status == ExamStatus.NotStarted && !hasData)
        {
            dbContext.Exams.Remove(exam);
        }
        else
        {
            exam.IsDeleted = true;
            exam.LastModifiedBy = actorUserId;
            exam.LastModifiedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync(AuditEventType.ExamDeleted, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
        return ExamOperationResult.NoContent();
    }

    private Task<ExamListItem> LoadItemAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Exams.AsNoTracking()
            .Where(exam => exam.Id == id)
            .Select(exam => new ExamListItem(
                exam.Id, exam.Name, exam.ExamTypeId, exam.ExamType!.Name, exam.Status.ToString(),
                exam.IsDeleted, exam.CreatedAtUtc, exam.LastModifiedAtUtc))
            .FirstAsync(cancellationToken);

    private static (string Name, List<string> Errors) Normalize(string? name)
    {
        var errors = new List<string>();
        var normalizedName = (name ?? string.Empty).Trim();

        if (normalizedName.Length == 0)
            errors.Add("Tên kỳ thi không được để trống.");
        else if (normalizedName.Length > Exam.NameMaxLength)
            errors.Add($"Tên kỳ thi không được vượt quá {Exam.NameMaxLength} ký tự.");

        return (normalizedName, errors);
    }
}
