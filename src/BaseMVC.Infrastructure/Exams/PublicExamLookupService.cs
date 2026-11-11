using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Exams;

public sealed class PublicExamLookupService(ApplicationDbContext dbContext) : IPublicExamLookupService
{
    public async Task<IReadOnlyList<PublicExamTypeOption>> ListTypesAsync(
        PublicExamLookupKind kind, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
            return [];

        var importKind = ToImportKind(kind);
        var examTypes = await dbContext.ExamTypes.AsNoTracking()
            .Where(item => item.DataFormat != null)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, Format = item.DataFormat })
            .ToListAsync(cancellationToken);

        return examTypes
            .Where(item => item.Format is { } format &&
                (format == ExamDataFormat.ItBasicCertificate ||
                    ExamDataSpecs.Find(format, importKind) is { SupportsStudentLookup: true }))
            .Select(item => new PublicExamTypeOption(
                item.Id,
                item.Name,
                item.Format == ExamDataFormat.ItBasicCertificate ? "Căn cước công dân" : "Mã sinh viên"))
            .ToArray();
    }

    public async Task<PublicExamLookupData?> LookupAsync(
        int examTypeId, PublicExamLookupKind kind, string identifier, DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind) || examTypeId <= 0)
            return null;

        var format = await dbContext.ExamTypes.AsNoTracking()
            .Where(item => item.Id == examTypeId)
            .Select(item => item.DataFormat)
            .SingleOrDefaultAsync(cancellationToken);
        if (format is null)
            return null;

        if (format == ExamDataFormat.ItBasicCertificate)
            return await LookupItBasicCertificateAsync(examTypeId, kind, identifier.Trim(), dateOfBirth, cancellationToken);

        var spec = ExamDataSpecs.Find(format.Value, ToImportKind(kind));
        if (spec is null || !spec.SupportsStudentLookup)
            return null;

        var normalizedCode = identifier.Trim().ToUpperInvariant();
        var studentId = await dbContext.Students.AsNoTracking()
            .Where(item => item.StudentCode == normalizedCode && item.DateOfBirth == dateOfBirth)
            .Select(item => (int?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (studentId is null)
            return null;

        return await spec.LookupStudentAsync(dbContext, examTypeId, studentId.Value, cancellationToken);
    }

    private async Task<PublicExamLookupData> LookupItBasicCertificateAsync(
        int examTypeId, PublicExamLookupKind kind, string identityNumber, DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        var normalizedIdentity = identityNumber.Trim();
        if (kind == PublicExamLookupKind.Schedule)
        {
            var rows = await dbContext.ItBasicCertificateCandidates.AsNoTracking()
                .Where(item => item.Exam!.ExamTypeId == examTypeId &&
                    item.IdentityNumber == normalizedIdentity && item.DateOfBirth == dateOfBirth)
                .OrderByDescending(item => item.ExamId)
                .ToListAsync(cancellationToken);
            var examNames = await GetExamNamesAsync(rows.Select(row => row.ExamId), cancellationToken);
            ExamDataColumn[] columns =
            [
                new("examName", "Kỳ thi"), new("identityNumber", "Căn cước công dân"),
                new("candidateNumber", "Số báo danh"), new("lastName", "Họ đệm"), new("firstName", "Tên"),
                new("examDate", "Ngày thi"), new("session", "Ca thi"), new("examDuration", "Thời gian thi"),
                new("examRoom", "Phòng thi")
            ];
            var items = rows.Select(row => new PublicExamLookupItem(
                examNames.GetValueOrDefault(row.ExamId, string.Empty),
                new Dictionary<string, string?>
                {
                    ["identityNumber"] = row.IdentityNumber,
                    ["candidateNumber"] = row.CandidateNumber,
                    ["lastName"] = row.LastName,
                    ["firstName"] = row.FirstName,
                    ["examDate"] = row.ExamDate?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    ["session"] = row.Session,
                    ["examDuration"] = row.ExamDuration,
                    ["examRoom"] = row.ExamRoom
                })).ToArray();
            return new PublicExamLookupData(columns, items);
        }

        var results = await dbContext.ItBasicCertificateResults.AsNoTracking()
            .Where(item => item.Exam!.ExamTypeId == examTypeId &&
                item.IdentityNumber == normalizedIdentity && item.DateOfBirth == dateOfBirth)
            .OrderByDescending(item => item.ExamId)
            .ToListAsync(cancellationToken);
        var resultExamNames = await GetExamNamesAsync(results.Select(row => row.ExamId), cancellationToken);
        ExamDataColumn[] resultColumns =
        [
            new("examName", "Kỳ thi"), new("identityNumber", "Căn cước công dân"),
            new("candidateNumber", "Số báo danh"), new("lastName", "Họ đệm"), new("firstName", "Tên"),
            new("multipleChoiceScore", "Điểm trắc nghiệm"), new("practicalScore", "Điểm thực hành"),
            new("result", "Kết quả"), new("note", "Ghi chú")
        ];
        var resultItems = results.Select(row => new PublicExamLookupItem(
            resultExamNames.GetValueOrDefault(row.ExamId, string.Empty),
            new Dictionary<string, string?>
            {
                ["identityNumber"] = row.IdentityNumber,
                ["candidateNumber"] = row.CandidateNumber,
                ["lastName"] = row.LastName,
                ["firstName"] = row.FirstName,
                ["multipleChoiceScore"] = row.MultipleChoiceScore,
                ["practicalScore"] = row.PracticalScore,
                ["result"] = row.Result,
                ["note"] = row.Note
            })).ToArray();
        return new PublicExamLookupData(resultColumns, resultItems);
    }

    private async Task<Dictionary<int, string>> GetExamNamesAsync(
        IEnumerable<int> examIds, CancellationToken cancellationToken)
    {
        var ids = examIds.Distinct().ToArray();
        return await dbContext.Exams.AsNoTracking().Where(exam => ids.Contains(exam.Id))
            .ToDictionaryAsync(exam => exam.Id, exam => exam.Name, cancellationToken);
    }

    private static ExamImportKind ToImportKind(PublicExamLookupKind kind) => kind switch
    {
        PublicExamLookupKind.Schedule => ExamImportKind.Candidates,
        PublicExamLookupKind.Results => ExamImportKind.Results,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
