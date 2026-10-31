using System.Globalization;
using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Exams;

public sealed class ExamImportService(ApplicationDbContext dbContext, IAuditLogWriter auditLogWriter) : IExamImportService
{
    public const int MaxDataRows = 10000;
    private const string ExamNotFoundMessage = "Không tìm thấy kỳ thi.";

    private static readonly string[] CandidateHeaders =
        ["STT", "StudentCode", "LastName", "FirstName", "ExamDate", "Session", "ExamDuration", "ExamRoom", "RetentionNote", "RetainedSection"];

    private static readonly string[] ResultHeaders =
        ["STT", "StudentCode", "LastName", "FirstName", "MultipleChoiceScore", "PracticalScore", "FinalScore", "Note"];

    private static readonly string[] DateFormats = ["d/M/yyyy", "d/M/yy"];

    public byte[] BuildTemplate(ExamImportFormat format, ExamImportKind kind)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(kind == ExamImportKind.Candidates ? "Candidates" : "Results");
        var headers = kind == ExamImportKind.Candidates ? CandidateHeaders : ResultHeaders;
        string[] sample = kind == ExamImportKind.Candidates
            ? ["1", "CTTT-10210104", "Nguyễn Văn", "An", "20/11/2026", "Ca 1", "9h30-11h30", "P.101", "", ""]
            : ["1", "CTTT-10210104", "Nguyễn Văn", "An", "7.5", "8", "75", ""];

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).SetValue(headers[i]);
            // Text format keeps dates, scores and codes exactly as typed.
            sheet.Column(i + 1).Style.NumberFormat.Format = "@";
            sheet.Cell(2, i + 1).SetValue(sample[i]);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ExamImportOutcome> ImportAsync(
        int examId, ExamImportFormat format, ExamImportKind kind, ExamImportMode mode,
        Stream content, string? actorUserId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.FirstOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null) return ExamImportOutcome.NotFound(ExamNotFoundMessage);

        if (kind == ExamImportKind.Candidates && exam.Status > ExamStatus.AwaitingSchedule)
            return ExamImportOutcome.Conflict("Chỉ được nhập danh sách thi khi kỳ thi ở trạng thái \"Chưa bắt đầu\", \"Mở đăng ký\", \"Đã hoàn thành đăng ký\" hoặc \"Chờ xếp lịch thi\".");
        if (kind == ExamImportKind.Results && exam.Status > ExamStatus.Taken)
            return ExamImportOutcome.Conflict("Chỉ được nhập kết quả thi khi kỳ thi ở trạng thái \"Chưa bắt đầu\", \"Mở đăng ký\", \"Đã hoàn thành đăng ký\", \"Chờ xếp lịch thi\", \"Đã xếp lịch thi\" hoặc \"Đã thi (Chờ kết quả)\".");

        IXLWorksheet worksheet;
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
            worksheet = workbook.Worksheets.First();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ExamImportOutcome.BadRequest("Không thể đọc file. Vui lòng dùng file Excel (.xlsx) hợp lệ.");
        }

        using (workbook)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxDataRows)
                return ExamImportOutcome.BadRequest($"File chỉ được chứa tối đa {MaxDataRows} dòng dữ liệu.");

            var outcome = kind == ExamImportKind.Candidates
                ? await ImportCoreAsync(
                    exam, mode, worksheet, lastRow, CandidateHeaders.Length, dbContext.ItExamCandidates,
                    (row, errors) => ParseCandidate(row, errors, exam.Id, actorUserId),
                    (candidate, studentId) => candidate.StudentId = studentId, actorUserId, cancellationToken,
                    updateExam: () => exam.Status = ExamStatus.ScheduleArranged)
                : await ImportCoreAsync(
                    exam, mode, worksheet, lastRow, ResultHeaders.Length, dbContext.ItExamResults,
                    (row, errors) => ParseResult(row, errors, exam.Id, actorUserId),
                    (result, studentId) => result.StudentId = studentId, actorUserId, cancellationToken,
                    updateExam: () => exam.Status = ExamStatus.ResultsEntered);

            if (outcome.StatusCode == 200 && outcome.Result!.ImportedCount > 0)
            {
                await auditLogWriter.WriteAsync(
                    kind == ExamImportKind.Candidates ? AuditEventType.ExamCandidatesImported : AuditEventType.ExamResultsImported,
                    AuditOutcome.Succeeded, actorUserId, cancellationToken: cancellationToken);
            }

            return outcome;
        }
    }

    public async Task<ExamSummaryDto?> GetSummaryAsync(int examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking()
            .Where(item => item.Id == examId)
            .Select(item => new { item.Id, item.Name, item.ExamTypeId, TypeName = item.ExamType!.Name, item.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (exam is null) return null;

        var candidates = await dbContext.ItExamCandidates.CountAsync(item => item.ExamId == examId, cancellationToken);
        var results = await dbContext.ItExamResults.CountAsync(item => item.ExamId == examId, cancellationToken);
        return new ExamSummaryDto(exam.Id, exam.Name, exam.ExamTypeId, exam.TypeName, exam.Status.ToString(), candidates, results);
    }

    public async Task<ExamPagedResponse<ItExamCandidateDto>?> SearchItCandidatesAsync(
        int examId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await dbContext.Exams.AnyAsync(item => item.Id == examId, cancellationToken)) return null;

        var query = dbContext.ItExamCandidates.AsNoTracking().Include(item => item.Student).Where(item => item.ExamId == examId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Student!.StudentCode.ToLower().Contains(term)
                || item.Student.LastName.ToLower().Contains(term) || item.Student.FirstName.ToLower().Contains(term));
        }

        return await PageAsync(
            query.OrderBy(item => item.Id), page, pageSize, cancellationToken,
            item => new ItExamCandidateDto(
                item.Id, item.Student!.StudentCode, item.Student.LastName, item.Student.FirstName,
                item.ExamDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.Session, item.ExamDuration, item.ExamRoom, item.RetentionNote, item.RetainedSection));
    }

    public async Task<ExamPagedResponse<ItExamResultDto>?> SearchItResultsAsync(
        int examId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await dbContext.Exams.AnyAsync(item => item.Id == examId, cancellationToken)) return null;

        var query = dbContext.ItExamResults.AsNoTracking().Include(item => item.Student).Where(item => item.ExamId == examId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Student!.StudentCode.ToLower().Contains(term)
                || item.Student.LastName.ToLower().Contains(term) || item.Student.FirstName.ToLower().Contains(term));
        }

        return await PageAsync(
            query.OrderBy(item => item.Id), page, pageSize, cancellationToken,
            item => new ItExamResultDto(
                item.Id, item.Student!.StudentCode, item.Student.LastName, item.Student.FirstName,
                item.MultipleChoiceScore, item.PracticalScore, item.FinalScore, item.Note));
    }

    private static async Task<ExamPagedResponse<TDto>> PageAsync<TEntity, TDto>(
        IQueryable<TEntity> query, int page, int pageSize, CancellationToken cancellationToken,
        Func<TEntity, TDto> map)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new ExamPagedResponse<TDto>(rows.Select(map).ToList(), page, pageSize, totalCount, totalPages);
    }

    private async Task<ExamImportOutcome> ImportCoreAsync<TEntity>(
        Exam exam, ExamImportMode mode, IXLWorksheet worksheet, int lastRow, int columnCount,
        DbSet<TEntity> set, Func<IXLRow, List<string>, (TEntity? Entity, string Code)> parse, Action<TEntity, int> assignStudent,
        string? actorUserId, CancellationToken cancellationToken, Action? updateExam)
        where TEntity : class
    {
        var studentIds = (await dbContext.Students.AsNoTracking()
            .Select(student => new { student.Id, student.StudentCode }).ToListAsync(cancellationToken))
            .ToDictionary(student => student.StudentCode, student => student.Id, StringComparer.OrdinalIgnoreCase);

        var seenStudents = new HashSet<int>();
        if (mode == ExamImportMode.AddOnly)
        {
            var existing = await set.AsNoTracking()
                .Where(item => EF.Property<int>(item, nameof(ItExamResult.ExamId)) == exam.Id)
                .Select(item => EF.Property<int>(item, nameof(ItExamResult.StudentId)))
                .ToListAsync(cancellationToken);
            seenStudents.UnionWith(existing);
        }

        var errors = new List<ExamImportRowError>();
        var entities = new List<TEntity>();
        var totalRows = 0;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.Cells(1, columnCount).All(cell => cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.GetFormattedString())))
                continue;

            totalRows++;
            var rowErrors = new List<string>();
            var (entity, code) = parse(row, rowErrors);

            if (entity is not null && rowErrors.Count == 0)
            {
                if (!studentIds.TryGetValue(code, out var studentId))
                    rowErrors.Add($"Mã sinh viên \"{code}\" không tồn tại trong danh sách sinh viên.");
                else if (!seenStudents.Add(studentId))
                    rowErrors.Add($"Mã sinh viên \"{code}\" đã có trong kỳ thi hoặc bị trùng trong file.");
                else
                    assignStudent(entity, studentId);
            }

            if (rowErrors.Count > 0 || entity is null)
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (mode == ExamImportMode.ReplaceAll)
            {
                await set
                    .Where(item => EF.Property<int>(item, nameof(ItExamResult.ExamId)) == exam.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            set.AddRange(entities);
            if (updateExam is not null)
            {
                updateExam();
                exam.LastModifiedBy = actorUserId;
                exam.LastModifiedAtUtc = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return ExamImportOutcome.BadRequest("Dữ liệu đã thay đổi trong lúc import (trùng thí sinh hoặc kỳ thi bị xóa). Vui lòng thử lại.");
        }

        return ExamImportOutcome.Success(new ExamImportResult(totalRows, entities.Count, errors));
    }

    private static (ItExamCandidate? Entity, string Code) ParseCandidate(IXLRow row, List<string> errors, int examId, string? actorUserId)
    {
        var code = Required(row.Cell(2), "StudentCode", Student.StudentCodeMaxLength, errors);
        var examDate = ParseDate(row.Cell(5), "ExamDate", errors);
        var session = Optional(row.Cell(6), "Session", ItExamCandidate.ShortTextMaxLength, errors);
        var duration = Optional(row.Cell(7), "ExamDuration", ItExamCandidate.ShortTextMaxLength, errors);
        var room = Optional(row.Cell(8), "ExamRoom", ItExamCandidate.ShortTextMaxLength, errors);
        var retentionNote = Optional(row.Cell(9), "RetentionNote", ItExamCandidate.NoteMaxLength, errors);
        var retainedSection = Optional(row.Cell(10), "RetainedSection", ItExamCandidate.NoteMaxLength, errors);

        if (errors.Count > 0) return (null, code);
        return (new ItExamCandidate
        {
            ExamId = examId,
            ExamDate = examDate, Session = session, ExamDuration = duration, ExamRoom = room,
            RetentionNote = retentionNote, RetainedSection = retainedSection,
            CreatedBy = actorUserId, CreatedAtUtc = DateTimeOffset.UtcNow
        }, code);
    }

    private static (ItExamResult? Entity, string Code) ParseResult(IXLRow row, List<string> errors, int examId, string? actorUserId)
    {
        var code = Required(row.Cell(2), "StudentCode", Student.StudentCodeMaxLength, errors);
        var multipleChoice = Optional(row.Cell(5), "MultipleChoiceScore", ItExamResult.ScoreMaxLength, errors);
        var practical = Optional(row.Cell(6), "PracticalScore", ItExamResult.ScoreMaxLength, errors);
        var finalScore = Optional(row.Cell(7), "FinalScore", ItExamResult.ScoreMaxLength, errors);
        var note = Optional(row.Cell(8), "Note", ItExamResult.NoteMaxLength, errors);

        if (finalScore is not null && !IsScoreInRange(finalScore))
            errors.Add("FinalScore phải là số từ 0 đến 100.");

        if (errors.Count > 0) return (null, code);
        return (new ItExamResult
        {
            ExamId = examId,
            MultipleChoiceScore = multipleChoice, PracticalScore = practical, FinalScore = finalScore, Note = note,
            CreatedBy = actorUserId, CreatedAtUtc = DateTimeOffset.UtcNow
        }, code);
    }

    private static bool IsScoreInRange(string text) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && value >= 0 && value <= 100;

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();

    private static string Required(IXLCell cell, string column, int maxLength, List<string> errors)
    {
        var text = Text(cell);
        if (text.Length == 0)
            errors.Add($"{column} không được để trống.");
        else if (text.Length > maxLength)
            errors.Add($"{column} tối đa {maxLength} ký tự.");
        return text;
    }

    private static string? Optional(IXLCell cell, string column, int maxLength, List<string> errors)
    {
        var text = Text(cell);
        if (text.Length == 0) return null;
        if (text.Length > maxLength) errors.Add($"{column} tối đa {maxLength} ký tự.");
        return text;
    }

    private static DateOnly? ParseDate(IXLCell cell, string column, List<string> errors)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());

        var text = Text(cell);
        if (text.Length == 0) return null;
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        errors.Add($"{column} phải có định dạng dd/mm/yyyy hoặc dd/mm/yy.");
        return null;
    }
}
