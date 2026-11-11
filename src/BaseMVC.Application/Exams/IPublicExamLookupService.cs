namespace BaseMVC.Application.Exams;

public interface IPublicExamLookupService
{
    Task<IReadOnlyList<PublicExamTypeOption>> ListTypesAsync(
        PublicExamLookupKind kind, CancellationToken cancellationToken);

    Task<PublicExamLookupData?> LookupAsync(
        int examTypeId, PublicExamLookupKind kind, string studentCode, DateOnly dateOfBirth,
        CancellationToken cancellationToken);
}
