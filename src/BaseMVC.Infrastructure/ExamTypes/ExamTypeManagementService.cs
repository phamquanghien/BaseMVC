using BaseMVC.Application.ExamTypes;
using BaseMVC.Domain.ExamTypes;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.ExamTypes;

public sealed class ExamTypeManagementService(ApplicationDbContext dbContext, IAuditLogWriter auditLogWriter) : IExamTypeManagementService
{
    private const string DuplicateNameMessage = "Tên loại kỳ thi này đã tồn tại.";

    public async Task<ExamTypeSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        IQueryable<ExamType> query = dbContext.ExamTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive match translated to SQL (see SqliteUnicodeLowerInterceptor for SQLite Unicode support).
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(examType => examType.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = await query
            .OrderBy(examType => examType.Name).ThenBy(examType => examType.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ExamTypeSearchResponse(items.Select(ToListItem).ToArray(), page, pageSize, totalCount, totalPages);
    }

    public async Task<ExamTypeOperationResult> CreateAsync(string name, string? actorUserId, CancellationToken cancellationToken)
    {
        var (normalizedName, errors) = Normalize(name);
        if (errors.Count > 0) return ExamTypeOperationResult.BadRequest(errors);
        var lowerName = normalizedName.ToLowerInvariant();
        if (await dbContext.ExamTypes.AnyAsync(item => item.Name.ToLower() == lowerName, cancellationToken))
            return ExamTypeOperationResult.Conflict(DuplicateNameMessage);

        var examType = new ExamType
        {
            Name = normalizedName,
            CreatedBy = actorUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.ExamTypes.Add(examType);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(examType).State = EntityState.Detached;
            if (await dbContext.ExamTypes.AsNoTracking().AnyAsync(item => item.Name.ToLower() == lowerName, cancellationToken))
                return ExamTypeOperationResult.Conflict(DuplicateNameMessage);
            throw;
        }

        await auditLogWriter.WriteAsync(AuditEventType.ExamTypeCreated, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
        return ExamTypeOperationResult.Created(ToListItem(examType));
    }

    public async Task<ExamTypeOperationResult> EditAsync(int id, string name, string? actorUserId, CancellationToken cancellationToken)
    {
        var examType = await dbContext.ExamTypes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (examType is null) return ExamTypeOperationResult.NotFound("Không tìm thấy loại kỳ thi.");

        var (normalizedName, errors) = Normalize(name);
        if (errors.Count > 0) return ExamTypeOperationResult.BadRequest(errors);
        var lowerName = normalizedName.ToLowerInvariant();
        if (await dbContext.ExamTypes.AnyAsync(item => item.Name.ToLower() == lowerName && item.Id != id, cancellationToken))
            return ExamTypeOperationResult.Conflict(DuplicateNameMessage);

        examType.Name = normalizedName;
        examType.LastModifiedBy = actorUserId;
        examType.LastModifiedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(examType).State = EntityState.Detached;
            if (await dbContext.ExamTypes.AsNoTracking().AnyAsync(
                item => item.Name.ToLower() == lowerName && item.Id != id, cancellationToken))
                return ExamTypeOperationResult.Conflict(DuplicateNameMessage);
            throw;
        }

        await auditLogWriter.WriteAsync(AuditEventType.ExamTypeUpdated, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
        return ExamTypeOperationResult.Success(ToListItem(examType));
    }

    public async Task<ExamTypeOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var examType = await dbContext.ExamTypes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (examType is null) return ExamTypeOperationResult.NotFound("Không tìm thấy loại kỳ thi.");

        // Soft-deleted exams still reference the type, so they must be counted too.
        if (await dbContext.Exams.IgnoreQueryFilters().AnyAsync(exam => exam.ExamTypeId == id, cancellationToken))
            return ExamTypeOperationResult.Conflict("Không thể xóa loại kỳ thi đã có kỳ thi thuộc loại này.");

        dbContext.ExamTypes.Remove(examType);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync(AuditEventType.ExamTypeDeleted, AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
        return ExamTypeOperationResult.NoContent();
    }

    private static (string Name, List<string> Errors) Normalize(string? name)
    {
        var errors = new List<string>();
        var normalizedName = (name ?? string.Empty).Trim();

        if (normalizedName.Length == 0)
            errors.Add("Tên loại kỳ thi không được để trống.");
        else if (normalizedName.Length > ExamType.NameMaxLength)
            errors.Add($"Tên loại kỳ thi không được vượt quá {ExamType.NameMaxLength} ký tự.");

        return (normalizedName, errors);
    }

    private static ExamTypeListItem ToListItem(ExamType examType) =>
        new(examType.Id, examType.Name, examType.CreatedAtUtc, examType.LastModifiedAtUtc);
}
