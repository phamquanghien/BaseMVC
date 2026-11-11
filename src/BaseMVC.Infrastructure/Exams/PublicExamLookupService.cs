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
                ExamDataSpecs.Find(format, importKind) is { SupportsStudentLookup: true })
            .Select(item => new PublicExamTypeOption(item.Id, item.Name))
            .ToArray();
    }

    public async Task<PublicExamLookupData?> LookupAsync(
        int examTypeId, PublicExamLookupKind kind, string studentCode, DateOnly dateOfBirth,
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

        var spec = ExamDataSpecs.Find(format.Value, ToImportKind(kind));
        if (spec is null || !spec.SupportsStudentLookup)
            return null;

        var normalizedCode = studentCode.Trim().ToUpperInvariant();
        var studentId = await dbContext.Students.AsNoTracking()
            .Where(item => item.StudentCode == normalizedCode && item.DateOfBirth == dateOfBirth)
            .Select(item => (int?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (studentId is null)
            return null;

        return await spec.LookupStudentAsync(dbContext, examTypeId, studentId.Value, cancellationToken);
    }

    private static ExamImportKind ToImportKind(PublicExamLookupKind kind) => kind switch
    {
        PublicExamLookupKind.Schedule => ExamImportKind.Candidates,
        PublicExamLookupKind.Results => ExamImportKind.Results,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
