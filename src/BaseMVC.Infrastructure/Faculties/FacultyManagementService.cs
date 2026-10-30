using BaseMVC.Application.Faculties;
using BaseMVC.Domain.Faculties;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Faculties;

public sealed class FacultyManagementService(ApplicationDbContext dbContext) : IFacultyManagementService
{
    public async Task<FacultySearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var faculties = dbContext.Faculties.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            faculties = faculties.Where(faculty =>
                faculty.FacultyCode.Contains(term) || faculty.FacultyName.Contains(term));
        }

        var totalCount = await faculties.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = await faculties
            .OrderBy(faculty => faculty.FacultyCode)
            .ThenBy(faculty => faculty.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new FacultySearchResponse(items.Select(ToListItem).ToArray(), page, pageSize, totalCount, totalPages);
    }

    public async Task<FacultyOperationResult> CreateAsync(string facultyCode, string facultyName, string? actorUserId, CancellationToken cancellationToken)
    {
        var (code, name, errors) = Normalize(facultyCode, facultyName);
        if (errors.Count > 0) return FacultyOperationResult.BadRequest(errors);
        if (await dbContext.Faculties.AnyAsync(faculty => faculty.FacultyCode == code, cancellationToken))
            return FacultyOperationResult.Conflict("Mã khoa này đã tồn tại.");

        var faculty = new Faculty
        {
            FacultyCode = code,
            FacultyName = name,
            CreatedBy = actorUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.Faculties.Add(faculty);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(faculty).State = EntityState.Detached;
            if (await dbContext.Faculties.AsNoTracking().AnyAsync(
                existing => existing.FacultyCode == code, cancellationToken))
                return FacultyOperationResult.Conflict("Mã khoa này đã tồn tại.");
            throw;
        }

        return FacultyOperationResult.Created(ToListItem(faculty));
    }

    public async Task<FacultyOperationResult> EditAsync(int id, string facultyCode, string facultyName, string? actorUserId, CancellationToken cancellationToken)
    {
        var faculty = await dbContext.Faculties.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (faculty is null) return FacultyOperationResult.NotFound("Không tìm thấy khoa.");

        var (code, name, errors) = Normalize(facultyCode, facultyName);
        if (errors.Count > 0) return FacultyOperationResult.BadRequest(errors);
        if (await dbContext.Faculties.AnyAsync(item => item.FacultyCode == code && item.Id != id, cancellationToken))
            return FacultyOperationResult.Conflict("Mã khoa này đã tồn tại.");

        faculty.FacultyCode = code;
        faculty.FacultyName = name;
        faculty.LastModifiedBy = actorUserId;
        faculty.LastModifiedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(faculty).State = EntityState.Detached;
            if (await dbContext.Faculties.AsNoTracking().AnyAsync(
                existing => existing.FacultyCode == code && existing.Id != id, cancellationToken))
                return FacultyOperationResult.Conflict("Mã khoa này đã tồn tại.");
            throw;
        }

        return FacultyOperationResult.Success(ToListItem(faculty));
    }

    public async Task<FacultyOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var faculty = await dbContext.Faculties.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (faculty is null) return FacultyOperationResult.NotFound("Không tìm thấy khoa.");

        dbContext.Faculties.Remove(faculty);
        await dbContext.SaveChangesAsync(cancellationToken);
        return FacultyOperationResult.NoContent();
    }

    private static (string Code, string Name, List<string> Errors) Normalize(string? facultyCode, string? facultyName)
    {
        var errors = new List<string>();
        var code = (facultyCode ?? string.Empty).Trim().ToUpperInvariant();
        var name = (facultyName ?? string.Empty).Trim();

        if (code.Length != Faculty.FacultyCodeLength || !code.All(char.IsAsciiLetterOrDigit))
            errors.Add($"Mã khoa phải gồm đúng {Faculty.FacultyCodeLength} ký tự chữ hoặc số.");
        if (name.Length == 0)
            errors.Add("Tên khoa không được để trống.");
        else if (name.Length > Faculty.FacultyNameMaxLength)
            errors.Add($"Tên khoa không được vượt quá {Faculty.FacultyNameMaxLength} ký tự.");

        return (code, name, errors);
    }

    private static FacultyListItem ToListItem(Faculty faculty) =>
        new(faculty.Id, faculty.FacultyCode, faculty.FacultyName, faculty.CreatedAtUtc, faculty.LastModifiedAtUtc);
}
