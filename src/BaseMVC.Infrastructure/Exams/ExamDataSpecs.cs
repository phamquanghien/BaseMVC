using System.Globalization;
using System.Reflection;
using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Exams;

internal enum ColType
{
    Text,
    Date,
    Gender,
    Score100,
    StudentCode,
    Ignored
}

/// <summary>Một cột của file Excel, đọc theo vị trí. Property null nghĩa là cột bị bỏ qua (STT, họ tên lấy từ Student).</summary>
internal sealed record Col(string Header, string? Property, string Label, string Sample, ColType Type = ColType.Text, bool Required = false);

internal interface IExamDataSpec
{
    ExamDataFormat Format { get; }

    ExamImportKind Kind { get; }

    IReadOnlyList<Col> Cols { get; }

    Task<ExamImportOutcome> ImportAsync(
        ApplicationDbContext db, Exam exam, ExamImportMode mode, IXLWorksheet worksheet, int lastRow,
        string? actorUserId, CancellationToken cancellationToken);

    Task<ExamDataPage> SearchAsync(
        ApplicationDbContext db, int examId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountAsync(ApplicationDbContext db, int examId, CancellationToken cancellationToken);

    byte[] BuildTemplate();

    Task<bool> ReferencesStudentAsync(ApplicationDbContext db, int studentId, CancellationToken cancellationToken);

    Task<bool> AnyForExamsAsync(ApplicationDbContext db, IReadOnlyCollection<int> examIds, CancellationToken cancellationToken);
}

internal sealed class ExamDataSpec<T>(ExamDataFormat format, ExamImportKind kind, IReadOnlyList<Col> cols) : IExamDataSpec
    where T : ExamRecord, new()
{
    private static readonly string[] DateFormats = ["d/M/yyyy", "d/M/yy"];
    private static readonly bool IsStudentBased = typeof(StudentExamRecord).IsAssignableFrom(typeof(T));

    public ExamDataFormat Format => format;

    public ExamImportKind Kind => kind;

    public IReadOnlyList<Col> Cols => cols;

    public Task<int> CountAsync(ApplicationDbContext db, int examId, CancellationToken cancellationToken) =>
        db.Set<T>().CountAsync(item => item.ExamId == examId, cancellationToken);

    public Task<bool> AnyForExamsAsync(ApplicationDbContext db, IReadOnlyCollection<int> examIds, CancellationToken cancellationToken) =>
        db.Set<T>().IgnoreQueryFilters().AnyAsync(item => examIds.Contains(item.ExamId), cancellationToken);

    public Task<bool> ReferencesStudentAsync(ApplicationDbContext db, int studentId, CancellationToken cancellationToken) =>
        IsStudentBased
            ? db.Set<T>().IgnoreQueryFilters().AnyAsync(item => EF.Property<int>(item, nameof(StudentExamRecord.StudentId)) == studentId, cancellationToken)
            : Task.FromResult(false);

    public async Task<ExamDataPage> SearchAsync(
        ApplicationDbContext db, int examId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Set<T>().AsNoTracking().Where(item => item.ExamId == examId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (IsStudentBased)
            {
                var matches = db.Students.Where(student => student.StudentCode.ToLower().Contains(term)
                    || student.LastName.ToLower().Contains(term) || student.FirstName.ToLower().Contains(term))
                    .Select(student => student.Id);
                query = query.Where(item => matches.Contains(EF.Property<int>(item, nameof(StudentExamRecord.StudentId))));
            }
            else
            {
                query = query.Where(item => EF.Property<string>(item, "IdentityNumber").ToLower().Contains(term)
                    || EF.Property<string>(item, "CandidateNumber").ToLower().Contains(term)
                    || EF.Property<string>(item, "LastName").ToLower().Contains(term)
                    || EF.Property<string>(item, "FirstName").ToLower().Contains(term));
            }
        }

        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var rows = await query.OrderBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var students = new Dictionary<int, Student>();
        if (IsStudentBased && rows.Count > 0)
        {
            var ids = rows.Select(row => ((StudentExamRecord)(ExamRecord)row).StudentId).Distinct().ToList();
            students = await db.Students.AsNoTracking().Where(student => ids.Contains(student.Id))
                .ToDictionaryAsync(student => student.Id, cancellationToken);
        }

        var displayCols = DisplayCols();
        var items = rows.Select(row =>
        {
            var values = new Dictionary<string, string?>();
            if (IsStudentBased)
            {
                var student = students.GetValueOrDefault(((StudentExamRecord)(ExamRecord)row).StudentId);
                values["studentCode"] = student?.StudentCode;
                values["lastName"] = student?.LastName;
                values["firstName"] = student?.FirstName;
            }

            foreach (var col in displayCols)
                values[Key(col.Property!)] = Display(Prop(col.Property!).GetValue(row));
            return (IReadOnlyDictionary<string, string?>)values;
        }).ToList();

        var columns = new List<ExamDataColumn>();
        if (IsStudentBased)
            columns.AddRange([new("studentCode", "Mã sinh viên"), new("lastName", "Họ lót"), new("firstName", "Tên")]);
        columns.AddRange(displayCols.Select(col => new ExamDataColumn(Key(col.Property!), col.Label)));
        return new ExamDataPage(columns, items, page, pageSize, totalCount, totalPages);
    }

    public async Task<ExamImportOutcome> ImportAsync(
        ApplicationDbContext db, Exam exam, ExamImportMode mode, IXLWorksheet worksheet, int lastRow,
        string? actorUserId, CancellationToken cancellationToken)
    {
        var set = db.Set<T>();
        var studentIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (IsStudentBased)
        {
            studentIds = (await db.Students.AsNoTracking()
                .Select(student => new { student.Id, student.StudentCode }).ToListAsync(cancellationToken))
                .ToDictionary(student => student.StudentCode, student => student.Id, StringComparer.OrdinalIgnoreCase);
        }

        var seenStudents = new HashSet<int>();
        var seenIdentity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCandidate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mode == ExamImportMode.AddOnly)
        {
            var existing = set.AsNoTracking().Where(item => item.ExamId == exam.Id);
            if (IsStudentBased)
            {
                seenStudents.UnionWith(await existing
                    .Select(item => EF.Property<int>(item, nameof(StudentExamRecord.StudentId))).ToListAsync(cancellationToken));
            }
            else
            {
                seenIdentity.UnionWith(await existing.Select(item => EF.Property<string>(item, "IdentityNumber")).ToListAsync(cancellationToken));
                seenCandidate.UnionWith(await existing.Select(item => EF.Property<string>(item, "CandidateNumber")).ToListAsync(cancellationToken));
            }
        }

        var errors = new List<ExamImportRowError>();
        var entities = new List<T>();
        var totalRows = 0;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.Cells(1, cols.Count).All(cell => cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetFormattedString())))
                continue;

            totalRows++;
            var rowErrors = new List<string>();
            var entity = new T { ExamId = exam.Id, CreatedBy = actorUserId, CreatedAtUtc = DateTimeOffset.UtcNow };
            var studentCode = string.Empty;

            for (var index = 0; index < cols.Count; index++)
            {
                var col = cols[index];
                var cell = row.Cell(index + 1);
                switch (col.Type)
                {
                    case ColType.Ignored:
                        break;
                    case ColType.StudentCode:
                        studentCode = ReadText(cell, col, rowErrors) ?? string.Empty;
                        break;
                    case ColType.Date:
                        var date = ParseDate(cell, col.Header, rowErrors);
                        if (date is null && col.Required && string.IsNullOrWhiteSpace(cell.GetFormattedString()))
                            rowErrors.Add($"{col.Header} không được để trống.");
                        Set(entity, col.Property!, date);
                        break;
                    case ColType.Gender:
                        Set(entity, col.Property!, ParseGender(cell, col.Header, rowErrors));
                        break;
                    default:
                        var text = ReadText(cell, col, rowErrors);
                        if (col.Type == ColType.Score100 && text is not null && !IsScoreInRange(text))
                            rowErrors.Add($"{col.Header} phải là số từ 0 đến 100.");
                        Set(entity, col.Property!, col.Required ? text ?? string.Empty : text);
                        break;
                }
            }

            if (rowErrors.Count == 0)
                CheckUnique(entity, studentCode, studentIds, seenStudents, seenIdentity, seenCandidate, rowErrors);

            if (rowErrors.Count > 0)
            {
                errors.Add(new ExamImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            entities.Add(entity);
        }

        if (totalRows == 0)
            return ExamImportOutcome.BadRequest("File không có dòng dữ liệu nào (dòng đầu tiên là tiêu đề).");

        // With no valid row there is nothing to save, so existing data is never wiped by a bad file.
        if (entities.Count == 0)
            return ExamImportOutcome.Success(new ExamImportResult(totalRows, 0, errors));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (mode == ExamImportMode.ReplaceAll)
                await set.Where(item => item.ExamId == exam.Id).ExecuteDeleteAsync(cancellationToken);

            set.AddRange(entities);
            exam.Status = kind == ExamImportKind.Candidates ? ExamStatus.ScheduleArranged : ExamStatus.ResultsEntered;
            exam.LastModifiedBy = actorUserId;
            exam.LastModifiedAtUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ExamImportOutcome.BadRequest("Dữ liệu đã thay đổi trong lúc import (trùng thí sinh hoặc kỳ thi bị xóa). Vui lòng thử lại.");
        }

        return ExamImportOutcome.Success(new ExamImportResult(totalRows, entities.Count, errors));
    }

    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(kind == ExamImportKind.Candidates ? "Candidates" : "Results");
        for (var i = 0; i < cols.Count; i++)
        {
            sheet.Cell(1, i + 1).SetValue(cols[i].Header);
            // Text format keeps dates, scores and codes exactly as typed.
            sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).SetValue(cols[i].Sample);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void CheckUnique(
        T entity, string studentCode, Dictionary<string, int> studentIds, HashSet<int> seenStudents,
        HashSet<string> seenIdentity, HashSet<string> seenCandidate, List<string> errors)
    {
        if (IsStudentBased)
        {
            if (!studentIds.TryGetValue(studentCode, out var studentId))
                errors.Add($"Mã sinh viên \"{studentCode}\" không tồn tại trong danh sách sinh viên.");
            else if (!seenStudents.Add(studentId))
                errors.Add($"Mã sinh viên \"{studentCode}\" đã có trong kỳ thi hoặc bị trùng trong file.");
            else
                ((StudentExamRecord)(object)entity).StudentId = studentId;
            return;
        }

        var identity = (string)Prop("IdentityNumber").GetValue(entity)!;
        var candidate = (string)Prop("CandidateNumber").GetValue(entity)!;
        if (seenIdentity.Contains(identity))
            errors.Add($"Số CCCD \"{identity}\" đã có trong kỳ thi hoặc bị trùng trong file.");
        if (seenCandidate.Contains(candidate))
            errors.Add($"Số báo danh \"{candidate}\" đã có trong kỳ thi hoặc bị trùng trong file.");
        if (errors.Count == 0)
        {
            seenIdentity.Add(identity);
            seenCandidate.Add(candidate);
        }
    }

    private IEnumerable<Col> DisplayCols() =>
        cols.Where(col => col.Property is not null && col.Type != ColType.StudentCode && col.Type != ColType.Ignored);

    private static string Key(string property) => char.ToLowerInvariant(property[0]) + property[1..];

    private static PropertyInfo Prop(string name) => typeof(T).GetProperty(name)!;

    private static void Set(T entity, string property, object? value) => Prop(property).SetValue(entity, value);

    private static string? Display(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        bool gender => gender ? "Nam" : "Nữ",
        _ => value.ToString()
    };

    private static string? ReadText(IXLCell cell, Col col, List<string> errors)
    {
        var text = cell.GetFormattedString().Trim();
        var max = col.Type == ColType.StudentCode ? Student.StudentCodeMaxLength : ExamDataLimits.For(col.Property ?? col.Header);
        if (text.Length == 0)
        {
            if (col.Required || col.Type == ColType.StudentCode) errors.Add($"{col.Header} không được để trống.");
            return null;
        }

        if (text.Length > max) errors.Add($"{col.Header} tối đa {max} ký tự.");
        return text;
    }

    private static DateOnly? ParseDate(IXLCell cell, string column, List<string> errors)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());

        var text = cell.GetFormattedString().Trim();
        if (text.Length == 0) return null;
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        errors.Add($"{column} phải có định dạng dd/mm/yyyy hoặc dd/mm/yy.");
        return null;
    }

    private static bool ParseGender(IXLCell cell, string column, List<string> errors)
    {
        var text = cell.GetFormattedString().Trim().ToLowerInvariant();
        switch (text)
        {
            case "nam" or "true" or "1":
                return true;
            case "nữ" or "nu" or "false" or "0":
                return false;
            default:
                errors.Add(text.Length == 0 ? $"{column} không được để trống." : $"{column} phải là \"Nam\" hoặc \"Nữ\".");
                return false;
        }
    }

    private static bool IsScoreInRange(string text) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && value >= 0 && value <= 100;
}

internal static class ExamDataSpecs
{
    private static readonly Col[] StudentPrefix =
    [
        new("STT", null, "STT", "1", ColType.Ignored),
        new("StudentCode", null, "Mã sinh viên", "CTTT-10210104", ColType.StudentCode),
        new("LastName", null, "Họ lót", "Nguyễn Văn", ColType.Ignored),
        new("FirstName", null, "Tên", "An", ColType.Ignored)
    ];

    private static readonly Col[] IdentityPrefix =
    [
        new("STT", null, "STT", "1", ColType.Ignored)
    ];

    private static readonly Col[] IdentityPerson =
    [
        new("LastName", "LastName", "Họ lót", "Nguyễn Văn", Required: true),
        new("FirstName", "FirstName", "Tên", "An", Required: true),
        new("DateOfBirth", "DateOfBirth", "Ngày sinh", "01/01/2005", ColType.Date, true),
        new("Gender", "Gender", "Giới tính", "Nam", ColType.Gender, true),
        new("Ethnicity", "Ethnicity", "Dân tộc", "Kinh"),
        new("BirthProvince", "BirthProvince", "Nơi sinh", "Hà Nội")
    ];

    private static readonly Col[] SpeakingSchedule =
    [
        new("ExamDateSpeaking", "ExamDateSpeaking", "Ngày thi nói", "20/11/2026", ColType.Date),
        new("SessionSpeaking", "SessionSpeaking", "Ca thi nói", "Ca 1"),
        new("ExamRoomSpeaking", "ExamRoomSpeaking", "Phòng thi nói", "P.102")
    ];

    private static readonly Col[] LanguageScores =
    [
        new("Listening", "Listening", "Nghe", "7.5"),
        new("Reading", "Reading", "Đọc", "8"),
        new("Writing", "Writing", "Viết", "7"),
        new("Speaking", "Speaking", "Nói", "7.5"),
        new("FinalScore", "FinalScore", "Điểm tổng", "7.5"),
        new("Result", "Result", "Kết quả", "Đạt"),
        new("Note", "Note", "Ghi chú", "")
    ];

    private static readonly IExamDataSpec[] All =
    [
        new ExamDataSpec<ItExamCandidate>(ExamDataFormat.ItStandard, ExamImportKind.Candidates,
        [
            .. StudentPrefix,
            new("ExamDate", "ExamDate", "Ngày thi", "20/11/2026", ColType.Date),
            new("Session", "Session", "Ca thi", "Ca 1"),
            new("ExamDuration", "ExamDuration", "Thời gian thi", "9h30-11h30"),
            new("ExamRoom", "ExamRoom", "Phòng thi", "P.101"),
            new("RetentionNote", "RetentionNote", "Bảo lưu", ""),
            new("RetainedSection", "RetainedSection", "Phần bảo lưu", "")
        ]),
        new ExamDataSpec<ItExamResult>(ExamDataFormat.ItStandard, ExamImportKind.Results,
        [
            .. StudentPrefix,
            new("MultipleChoiceScore", "MultipleChoiceScore", "Điểm trắc nghiệm", "7.5"),
            new("PracticalScore", "PracticalScore", "Điểm thực hành", "8"),
            new("FinalScore", "FinalScore", "Điểm tổng", "75", ColType.Score100),
            new("Note", "Note", "Ghi chú", "")
        ]),
        new ExamDataSpec<LanguageStandardCandidate>(ExamDataFormat.LanguageStandard, ExamImportKind.Candidates,
        [
            .. StudentPrefix,
            new("RetentionExam", "RetentionExam", "Bảo lưu kỳ thi", ""),
            new("RetainedSection", "RetainedSection", "Phần bảo lưu", ""),
            new("Note", "Note", "Ghi chú", ""),
            new("ExamDate", "ExamDate", "Ngày thi", "20/11/2026", ColType.Date),
            new("Session", "Session", "Ca thi", "Ca 1"),
            new("ExamRoom", "ExamRoom", "Phòng thi", "P.101"),
            .. SpeakingSchedule
        ]),
        new ExamDataSpec<LanguageStandardResult>(ExamDataFormat.LanguageStandard, ExamImportKind.Results,
            [.. StudentPrefix, .. LanguageScores]),
        new ExamDataSpec<LanguagePlacementCandidate>(ExamDataFormat.LanguagePlacement, ExamImportKind.Candidates,
        [
            .. StudentPrefix,
            new("Note", "Note", "Ghi chú", ""),
            new("ExamDate", "ExamDate", "Ngày thi", "20/11/2026", ColType.Date),
            new("Session", "Session", "Ca thi", "Ca 1"),
            new("ExamRoom", "ExamRoom", "Phòng thi", "P.101"),
            .. SpeakingSchedule
        ]),
        new ExamDataSpec<LanguagePlacementResult>(ExamDataFormat.LanguagePlacement, ExamImportKind.Results,
            [.. StudentPrefix, .. LanguageScores]),
        new ExamDataSpec<EnglishIntensiveResult>(ExamDataFormat.EnglishIntensive, ExamImportKind.Results,
        [
            .. StudentPrefix,
            new("Cohort", "Cohort", "Khóa", "K20"),
            new("Attendance", "Attendance", "Chuyên cần", "10"),
            new("FinalScore", "FinalScore", "Điểm tổng", "8"),
            new("Group", "Group", "Nhóm", "01"),
            new("Note", "Note", "Ghi chú", "")
        ]),
        new ExamDataSpec<ItBasicCertificateCandidate>(ExamDataFormat.ItBasicCertificate, ExamImportKind.Candidates,
        [
            .. IdentityPrefix,
            new("IdentityNumber", "IdentityNumber", "Số CCCD", "001205000001", Required: true),
            new("CandidateNumber", "CandidateNumber", "Số báo danh", "CC0001", Required: true),
            .. IdentityPerson,
            new("Note", "Note", "Ghi chú", ""),
            new("ExamDate", "ExamDate", "Ngày thi", "20/11/2026", ColType.Date),
            new("Session", "Session", "Ca thi", "Ca 1"),
            new("ExamDuration", "ExamDuration", "Thời gian thi", "09h00-11h00"),
            new("ExamRoom", "ExamRoom", "Phòng thi", "P.101")
        ]),
        new ExamDataSpec<ItBasicCertificateResult>(ExamDataFormat.ItBasicCertificate, ExamImportKind.Results,
        [
            .. IdentityPrefix,
            new("CandidateNumber", "CandidateNumber", "Số báo danh", "CC0001", Required: true),
            new("IdentityNumber", "IdentityNumber", "Số CCCD", "001205000001", Required: true),
            .. IdentityPerson,
            new("MultipleChoiceScore", "MultipleChoiceScore", "Điểm trắc nghiệm", "7.5"),
            new("PracticalScore", "PracticalScore", "Điểm thực hành", "8"),
            new("Result", "Result", "Kết quả", "Đạt"),
            new("Note", "Note", "Ghi chú", "")
        ])
    ];

    public static IReadOnlyList<IExamDataSpec> Specs => All;

    public static IExamDataSpec? Find(ExamDataFormat format, ExamImportKind kind) =>
        All.FirstOrDefault(spec => spec.Format == format && spec.Kind == kind);
}
