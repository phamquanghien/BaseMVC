namespace BaseMVC.Application.Students;

public interface IStudentImportService
{
    Task<StudentImportOutcome> ImportAsync(Stream content, string? actorUserId, CancellationToken cancellationToken);
}
