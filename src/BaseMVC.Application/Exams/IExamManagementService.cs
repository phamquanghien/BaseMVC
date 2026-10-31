namespace BaseMVC.Application.Exams;

public interface IExamManagementService
{
    Task<ExamSearchResponse> SearchAsync(string? search, int? examTypeId, string? status, bool includeDeleted, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExamTypeOption>> ListExamTypeOptionsAsync(CancellationToken cancellationToken);
    Task<ExamOperationResult> CreateAsync(string name, int examTypeId, string? actorUserId, CancellationToken cancellationToken);
    Task<ExamOperationResult> EditAsync(int id, string name, int examTypeId, string? actorUserId, CancellationToken cancellationToken);
    Task<ExamOperationResult> ChangeStatusAsync(int id, string status, string? actorUserId, CancellationToken cancellationToken);
    Task<ExamOperationResult> DeleteAsync(int id, string? actorUserId, CancellationToken cancellationToken);
}
