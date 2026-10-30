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
        // SQLite only ignores case for ASCII; filter in memory so Unicode (Vietnamese) is case-insensitive too.
        var all = await dbContext.Faculties.AsNoTracking()
            .OrderBy(faculty => faculty.FacultyCode)
            .ThenBy(faculty => faculty.Id)
            .ToListAsync(cancellationToken);
        IEnumerable<Faculty> faculties = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            faculties = all.Where(faculty =>
                faculty.FacultyCode.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                faculty.FacultyName.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        var matches = faculties.ToList();
        var totalCount = matches.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);
        var items = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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

        if (faculty.FacultyCode != code
            && await dbContext.Students.AnyAsync(student => student.FacultyCode == faculty.FacultyCode, cancellationToken))
            return FacultyOperationResult.Conflict("Không thể đổi mã khoa vì đã có sinh viên thuộc khoa này.");

        if (faculty.FacultyCode != code)
        {
            // FacultyCode is the principal key of Student.FacultyCode, so EF forbids changing it on a tracked entity.
            var modifiedAt = DateTimeOffset.UtcNow;
            try
            {
                await dbContext.Faculties.Where(item => item.Id == id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FacultyCode, code)
                    .SetProperty(item => item.FacultyName, name)
                    .SetProperty(item => item.LastModifiedBy, actorUserId)
                    .SetProperty(item => item.LastModifiedAtUtc, modifiedAt), cancellationToken);
            }
            catch (DbUpdateException)
            {
                if (await dbContext.Faculties.AsNoTracking().AnyAsync(
                    existing => existing.FacultyCode == code && existing.Id != id, cancellationToken))
                    return FacultyOperationResult.Conflict("Mã khoa này đã tồn tại.");
                throw;
            }

            dbContext.Entry(faculty).State = EntityState.Detached;
            return FacultyOperationResult.Success(
                new FacultyListItem(id, code, name, faculty.CreatedAtUtc, modifiedAt));
        }

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

        if (await dbContext.Students.AnyAsync(student => student.FacultyCode == faculty.FacultyCode, cancellationToken))
            return FacultyOperationResult.Conflict("Không thể xóa khoa vì đã có sinh viên thuộc khoa này.");

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
