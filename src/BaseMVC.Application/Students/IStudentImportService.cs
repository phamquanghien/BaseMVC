namespace BaseMVC.Application.Students;

public interface IStudentImportService
{
    byte[] BuildTemplate();

    Task<StudentImportOutcome> ImportAsync(Stream content, string? actorUserId, CancellationToken cancellationToken);
}
