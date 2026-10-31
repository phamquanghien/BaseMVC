using BaseMVC.Application.Students;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Students;

public sealed class StudentManagementService(ApplicationDbContext dbContext) : IStudentManagementService
{
    private const string DuplicateCodeMessage = "Mã sinh viên này đã tồn tại.";

    public async Task<StudentSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query =
            from student in dbContext.Students.AsNoTracking()
            join faculty in dbContext.Faculties.AsNoTracking() on student.FacultyCode equals faculty.FacultyCode into faculties
            from faculty in faculties.DefaultIfEmpty()
            select new { student, FacultyName = faculty == null ? null : faculty.FacultyName };

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive match translated to SQL (see SqliteUnicodeLowerInterceptor for SQLite Unicode support).
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(row =>
                row.student.StudentCode.ToLower().Contains(term) ||
                row.student.LastName.ToLower().Contains(term) ||
                row.student.FirstName.ToLower().Contains(term) ||
                (row.student.LastName + " " + row.student.FirstName).ToLower().Contains(term) ||
                row.student.ClassCode.ToLower().Contains(term) ||
                row.student.FacultyCode.ToLower().Contains(term) ||
                (row.FacultyName != null && row.FacultyName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        var items = await query
            .OrderBy(row => row.student.StudentCode).ThenBy(row => row.student.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(row => new StudentListItem(
                row.student.Id, row.student.StudentCode, row.student.LastName, row.student.FirstName,
                row.student.ClassCode, row.student.FacultyCode, row.FacultyName,
                row.student.Gender, row.student.EligibleForEnglish, row.student.PassedLanguageRequirement,
                row.student.PassedITRequirement, row.student.DateOfBirth, row.student.CreatedAtUtc,
                row.student.LastModifiedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new StudentSearchResponse(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<IReadOnlyList<FacultyOption>> ListFacultyOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Faculties.AsNoTracking()
            .OrderBy(faculty => faculty.FacultyCode)
            .Select(faculty => new FacultyOption(faculty.FacultyCode, faculty.FacultyName))
            .ToListAsync(cancellationToken);

    public async Task<StudentOperationResult> CreateAsync(StudentInput input, string? actorUserId, CancellationToken cancellationToken)
    {
        var (normalized, errors) = Normalize(input);
        if (errors.Count > 0) return StudentOperationResult.BadRequest(errors);
        if (!await FacultyExistsAsync(normalized.FacultyCode, cancellationToken))
            return StudentOperationResult.BadRequest(["Khoa không tồn tại."]);
        if (await dbContext.Students.AnyAsync(item => item.StudentCode == normalized.StudentCode, cancellationToken))
            return StudentOperationResult.Conflict(DuplicateCodeMessage);

        var student = new Student { CreatedBy = actorUserId, CreatedAtUtc = DateTimeOffset.UtcNow };
        Apply(student, normalized);
        dbContext.Students.Add(student);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(student).State = EntityState.Detached;
            if (await dbContext.Students.AsNoTracking().AnyAsync(
                existing => existing.StudentCode == normalized.StudentCode, cancellationToken))
                return StudentOperationResult.Conflict(DuplicateCodeMessage);
            throw;
        }

        return StudentOperationResult.Created(await ToListItemAsync(student, cancellationToken));
    }

    public async Task<StudentOperationResult> EditAsync(int id, StudentInput input, string? actorUserId, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null) return StudentOperationResult.NotFound("Không tìm thấy sinh viên.");

        var (normalized, errors) = Normalize(input);
        if (errors.Count > 0) return StudentOperationResult.BadRequest(errors);
        if (!await FacultyExistsAsync(normalized.FacultyCode, cancellationToken))
            return StudentOperationResult.BadRequest(["Khoa không tồn tại."]);
        if (await dbContext.Students.AnyAsync(
            item => item.StudentCode == normalized.StudentCode && item.Id != id, cancellationToken))
            return StudentOperationResult.Conflict(DuplicateCodeMessage);

        Apply(student, normalized);
        student.LastModifiedBy = actorUserId;
        student.LastModifiedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(student).State = EntityState.Detached;
            if (await dbContext.Students.AsNoTracking().AnyAsync(
                existing => existing.StudentCode == normalized.StudentCode && existing.Id != id, cancellationToken))
                return StudentOperationResult.Conflict(DuplicateCodeMessage);
            throw;
        }

        return StudentOperationResult.Success(await ToListItemAsync(student, cancellationToken));
    }

    public async Task<StudentOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null) return StudentOperationResult.NotFound("Không tìm thấy sinh viên.");

        dbContext.Students.Remove(student);
        await dbContext.SaveChangesAsync(cancellationToken);
        return StudentOperationResult.NoContent();
    }

    private Task<bool> FacultyExistsAsync(string facultyCode, CancellationToken cancellationToken) =>
        dbContext.Faculties.AnyAsync(faculty => faculty.FacultyCode == facultyCode, cancellationToken);

    internal static void Apply(Student student, StudentInput input)
    {
        student.StudentCode = input.StudentCode;
        student.LastName = input.LastName;
        student.FirstName = input.FirstName;
        student.ClassCode = input.ClassCode;
        student.FacultyCode = input.FacultyCode;
        student.Gender = input.Gender;
        student.EligibleForEnglish = input.EligibleForEnglish;
        student.PassedLanguageRequirement = input.PassedLanguageRequirement;
        student.PassedITRequirement = input.PassedITRequirement;
        student.DateOfBirth = input.DateOfBirth;
    }

    internal static (StudentInput Input, List<string> Errors) Normalize(StudentInput? input)
    {
        var errors = new List<string>();
        input ??= new StudentInput(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false, null);

        var studentCode = (input.StudentCode ?? string.Empty).Trim().ToUpperInvariant();
        var lastName = (input.LastName ?? string.Empty).Trim();
        var firstName = (input.FirstName ?? string.Empty).Trim();
        var classCode = (input.ClassCode ?? string.Empty).Trim();
        var facultyCode = (input.FacultyCode ?? string.Empty).Trim().ToUpperInvariant();

        if (studentCode.Length == 0 || studentCode.Length > Student.StudentCodeMaxLength || !studentCode.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            errors.Add($"Mã sinh viên phải gồm 1 đến {Student.StudentCodeMaxLength} ký tự chữ, số hoặc dấu gạch ngang (-).");
        if (lastName.Length == 0) errors.Add("Họ không được để trống.");
        else if (lastName.Length > Student.NameMaxLength) errors.Add($"Họ không được vượt quá {Student.NameMaxLength} ký tự.");
        if (firstName.Length == 0) errors.Add("Tên không được để trống.");
        else if (firstName.Length > Student.NameMaxLength) errors.Add($"Tên không được vượt quá {Student.NameMaxLength} ký tự.");
        if (classCode.Length == 0) errors.Add("Mã lớp không được để trống.");
        else if (classCode.Length > Student.ClassCodeMaxLength) errors.Add($"Mã lớp không được vượt quá {Student.ClassCodeMaxLength} ký tự.");
        if (facultyCode.Length != Student.FacultyCodeLength || !facultyCode.All(char.IsAsciiLetterOrDigit))
            errors.Add($"Mã khoa phải gồm đúng {Student.FacultyCodeLength} ký tự chữ hoặc số.");
        if (input.DateOfBirth is { } dateOfBirth && dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.Date))
            errors.Add("Ngày sinh không được ở tương lai.");

        return (input with
        {
            StudentCode = studentCode,
            LastName = lastName,
            FirstName = firstName,
            ClassCode = classCode,
            FacultyCode = facultyCode
        }, errors);
    }

    private async Task<StudentListItem> ToListItemAsync(Student student, CancellationToken cancellationToken)
    {
        var facultyName = await dbContext.Faculties.AsNoTracking()
            .Where(faculty => faculty.FacultyCode == student.FacultyCode)
            .Select(faculty => faculty.FacultyName)
            .FirstOrDefaultAsync(cancellationToken);

        return new StudentListItem(
            student.Id, student.StudentCode, student.LastName, student.FirstName, student.ClassCode,
            student.FacultyCode, facultyName, student.Gender, student.EligibleForEnglish,
            student.PassedLanguageRequirement, student.PassedITRequirement, student.DateOfBirth,
            student.CreatedAtUtc, student.LastModifiedAtUtc);
    }
}
