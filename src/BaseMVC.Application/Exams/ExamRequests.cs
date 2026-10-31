using MediatR;

namespace BaseMVC.Application.Exams;

public sealed record SearchExamsQuery(
    string? Search, int? ExamTypeId, string? Status, bool IncludeDeleted, int Page, int PageSize) : IRequest<ExamSearchResponse>;
public sealed record ListExamTypeOptionsQuery : IRequest<IReadOnlyList<ExamTypeOption>>;
public sealed record CreateExamCommand(string Name, int ExamTypeId, string? ActorUserId) : IRequest<ExamOperationResult>;
public sealed record EditExamCommand(int Id, string Name, int ExamTypeId, string? ActorUserId) : IRequest<ExamOperationResult>;
public sealed record ChangeExamStatusCommand(int Id, string Status, string? ActorUserId) : IRequest<ExamOperationResult>;
public sealed record DeleteExamCommand(int Id, string? ActorUserId) : IRequest<ExamOperationResult>;
