namespace BaseMVC.Application.Faculties;

public interface IFacultyManagementService
{
    Task<FacultySearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<FacultyOperationResult> CreateAsync(string facultyCode, string facultyName, string? actorUserId, CancellationToken cancellationToken);
    Task<FacultyOperationResult> EditAsync(int id, string facultyCode, string facultyName, string? actorUserId, CancellationToken cancellationToken);
    Task<FacultyOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken);
}
