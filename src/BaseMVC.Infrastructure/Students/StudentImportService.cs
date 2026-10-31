using System.Globalization;
using BaseMVC.Application.Students;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Students;

public sealed class StudentImportService(ApplicationDbContext dbContext) : IStudentImportService
{
    public const int MaxDataRows = 10000;
    private const int ColumnCount = 7;

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Students");
        var headers = new[] { "StudentCode", "LastName", "FirstName", "Gender", "DateOfBirth", "ClassCode", "FacultyCode" };
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).SetValue(headers[i]);
        sheet.Row(1).Style.Font.Bold = true;

        // Text format keeps dd/mm/yyyy and leading zeros intact when users type values.
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(5).Style.NumberFormat.Format = "@";
        sheet.Cell(2, 1).SetValue("CTTT-10210104");
        sheet.Cell(2, 2).SetValue("Nguyễn Văn");
        sheet.Cell(2, 3).SetValue("An");
        sheet.Cell(2, 4).SetValue("1");
        sheet.Cell(2, 5).SetValue("20/05/2004");
        sheet.Cell(2, 6).SetValue("CTTT01");
        sheet.Cell(2, 7).SetValue("CT");
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<StudentImportOutcome> ImportAsync(Stream content, string? actorUserId, CancellationToken cancellationToken)
    {
        IXLWorksheet worksheet;
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
            worksheet = workbook.Worksheets.First();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StudentImportOutcome.BadRequest("Không thể đọc file. Vui lòng dùng file Excel (.xlsx) hợp lệ.");
        }

        using (workbook)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxDataRows)
                return StudentImportOutcome.BadRequest($"File chỉ được chứa tối đa {MaxDataRows} dòng dữ liệu.");

            var faculties = (await dbContext.Faculties.AsNoTracking()
                .Select(faculty => faculty.FacultyCode).ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
            var seenCodes = (await dbContext.Students.AsNoTracking()
                .Select(student => student.StudentCode).ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            var errors = new List<StudentImportRowError>();
            var students = new List<Student>();
            var totalRows = 0;
            var now = DateTimeOffset.UtcNow;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.Cells(1, ColumnCount).All(cell => cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetFormattedString())))
                    continue;

                totalRows++;
                var rowErrors = new List<string>();
                var gender = ParseBool(row.Cell(4), "Gender", rowErrors);
                var dateOfBirth = ParseDate(row.Cell(5), rowErrors);

                var (input, validationErrors) = StudentManagementService.Normalize(new StudentInput(
                    Text(row.Cell(1)), Text(row.Cell(2)), Text(row.Cell(3)), Text(row.Cell(6)), Text(row.Cell(7)),
                    gender, false, false, false, dateOfBirth));
                rowErrors.AddRange(validationErrors);

                if (validationErrors.Count == 0)
                {
                    if (!faculties.Contains(input.FacultyCode))
                        rowErrors.Add($"Khoa \"{input.FacultyCode}\" không tồn tại.");
                    if (seenCodes.Contains(input.StudentCode))
                        rowErrors.Add($"Mã sinh viên \"{input.StudentCode}\" đã tồn tại hoặc bị trùng trong file.");
                }

                if (rowErrors.Count > 0)
                {
                    errors.Add(new StudentImportRowError(rowNumber, string.Join(" ", rowErrors)));
                    continue;
                }

                seenCodes.Add(input.StudentCode);
                var student = new Student { CreatedBy = actorUserId, CreatedAtUtc = now };
                StudentManagementService.Apply(student, input);
                students.Add(student);
            }

            if (totalRows == 0)
                return StudentImportOutcome.BadRequest("File không có dòng dữ liệu nào (dòng đầu tiên là tiêu đề).");

            if (students.Count > 0)
            {
                dbContext.Students.AddRange(students);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    dbContext.ChangeTracker.Clear();
                    return StudentImportOutcome.BadRequest("Dữ liệu đã thay đổi trong lúc import (trùng mã hoặc khoa bị xóa). Vui lòng thử lại.");
                }
            }

            return StudentImportOutcome.Success(new StudentImportResult(totalRows, students.Count, errors));
        }
    }

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();

    private static bool ParseBool(IXLCell cell, string column, List<string> errors)
    {
        if (cell.IsEmpty()) return false;
        if (cell.DataType == XLDataType.Boolean) return cell.GetBoolean();

        var text = Text(cell);
        if (text.Length == 0) return false;
        if (text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || text == "1") return true;
        if (text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || text == "0") return false;

        errors.Add($"{column} chỉ nhận TRUE/FALSE hoặc 1/0.");
        return false;
    }

    private static DateOnly? ParseDate(IXLCell cell, List<string> errors)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());

        var text = Text(cell);
        if (text.Length == 0) return null;
        if (DateOnly.TryParseExact(text, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        errors.Add("Ngày sinh phải có định dạng dd/mm/yyyy.");
        return null;
    }
}
