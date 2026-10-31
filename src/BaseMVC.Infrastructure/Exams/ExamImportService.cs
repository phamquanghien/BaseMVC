using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Exams;

public sealed class ExamImportService(ApplicationDbContext dbContext, IAuditLogWriter auditLogWriter) : IExamImportService
{
    public const int MaxDataRows = 10000;
    private const string ExamNotFoundMessage = "Không tìm thấy kỳ thi.";
    private const string NoFormatMessage = "Loại kỳ thi của kỳ thi này chưa được gán định dạng dữ liệu. Vui lòng gán định dạng ở mục Loại kỳ thi trước.";
    private const string UnsupportedMessage = "Định dạng dữ liệu của loại kỳ thi này không hỗ trợ loại dữ liệu đã chọn.";

    public async Task<ExamDataOutcome<byte[]>> BuildTemplateAsync(int examId, ExamImportKind kind, CancellationToken cancellationToken)
    {
        var resolved = await ResolveSpecAsync(examId, kind, cancellationToken);
        return resolved.Spec is null
            ? new ExamDataOutcome<byte[]>(resolved.StatusCode, Error: resolved.Error)
            : ExamDataOutcome<byte[]>.Success(resolved.Spec.BuildTemplate());
    }

    private async Task<(IExamDataSpec? Spec, int StatusCode, string? Error)> ResolveSpecAsync(
        int examId, ExamImportKind kind, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind)) return (null, 400, UnsupportedMessage);
        var row = await dbContext.Exams.AsNoTracking()
            .Where(item => item.Id == examId)
            .Select(item => new { Format = item.ExamType!.DataFormat })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return (null, 404, ExamNotFoundMessage);
        if (row.Format is null) return (null, 409, NoFormatMessage);
        var spec = ExamDataSpecs.Find(row.Format.Value, kind);
        return spec is null ? (null, 400, UnsupportedMessage) : (spec, 200, null);
    }

    public async Task<ExamImportOutcome> ImportAsync(
        int examId, ExamImportKind kind, ExamImportMode mode,
        Stream content, string? actorUserId, CancellationToken cancellationToken)
    {
        var resolved = await ResolveSpecAsync(examId, kind, cancellationToken);
        if (resolved.Spec is null) return new ExamImportOutcome(resolved.StatusCode, Error: resolved.Error);
        var spec = resolved.Spec;

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

            var outcome = await spec.ImportAsync(dbContext, exam, mode, worksheet, lastRow, actorUserId, cancellationToken);
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
            .Select(item => new { item.Id, item.Name, item.ExamTypeId, TypeName = item.ExamType!.Name, item.Status, Format = item.ExamType!.DataFormat })
            .FirstOrDefaultAsync(cancellationToken);
        if (exam is null) return null;

        var counts = new List<ExamDataCount>();
        foreach (var spec in ExamDataSpecs.Specs.Where(spec => spec.Format == exam.Format))
            counts.Add(new ExamDataCount(spec.Format.ToString(), spec.Kind.ToString(), await spec.CountAsync(dbContext, examId, cancellationToken)));
        var supportsCandidates = exam.Format is { } format && ExamDataSpecs.Find(format, ExamImportKind.Candidates) is not null;
        return new ExamSummaryDto(exam.Id, exam.Name, exam.ExamTypeId, exam.TypeName, exam.Status.ToString(), exam.Format?.ToString(), supportsCandidates, counts);
    }

    public async Task<ExamDataOutcome<ExamDataPage>> SearchDataAsync(
        int examId, ExamImportKind kind, string? search, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveSpecAsync(examId, kind, cancellationToken);
        if (resolved.Spec is null) return new ExamDataOutcome<ExamDataPage>(resolved.StatusCode, Error: resolved.Error);
        return ExamDataOutcome<ExamDataPage>.Success(
            await resolved.Spec.SearchAsync(dbContext, examId, search, page, pageSize, cancellationToken));
    }
}
