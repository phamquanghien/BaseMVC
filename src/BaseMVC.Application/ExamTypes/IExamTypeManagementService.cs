namespace BaseMVC.Application.ExamTypes;

public interface IExamTypeManagementService
{
    Task<ExamTypeSearchResponse> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<ExamTypeOperationResult> CreateAsync(string name, string? dataFormat, string? actorUserId, CancellationToken cancellationToken);
    Task<ExamTypeOperationResult> EditAsync(int id, string name, string? dataFormat, string? actorUserId, CancellationToken cancellationToken);
    Task<ExamTypeOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken);
}
