namespace BaseMVC.Application.Students;

public interface IStudentManagementService
{
    Task<StudentSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<FacultyOption>> ListFacultyOptionsAsync(CancellationToken cancellationToken);
    Task<StudentOperationResult> CreateAsync(StudentInput input, string? actorUserId, CancellationToken cancellationToken);
    Task<StudentOperationResult> EditAsync(int id, StudentInput input, string? actorUserId, CancellationToken cancellationToken);
    Task<StudentOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken);
}
