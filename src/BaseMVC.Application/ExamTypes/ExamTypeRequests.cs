using MediatR;

namespace BaseMVC.Application.ExamTypes;

public sealed record SearchExamTypesQuery(string? Search, int Page, int PageSize) : IRequest<ExamTypeSearchResponse>;
public sealed record CreateExamTypeCommand(string Name, string? ActorUserId) : IRequest<ExamTypeOperationResult>;
public sealed record EditExamTypeCommand(int Id, string Name, string? ActorUserId) : IRequest<ExamTypeOperationResult>;
public sealed record DeleteExamTypeCommand(int Id, string? ActorUserId) : IRequest<ExamTypeOperationResult>;
