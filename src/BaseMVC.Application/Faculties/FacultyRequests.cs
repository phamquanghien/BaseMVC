using MediatR;

namespace BaseMVC.Application.Faculties;

public sealed record SearchFacultiesQuery(string? Search, int Page, int PageSize) : IRequest<FacultySearchResponse>;
public sealed record CreateFacultyCommand(string FacultyCode, string FacultyName, string? ActorUserId) : IRequest<FacultyOperationResult>;
public sealed record EditFacultyCommand(int Id, string FacultyCode, string FacultyName, string? ActorUserId) : IRequest<FacultyOperationResult>;
public sealed record DeleteFacultyCommand(int Id, string? ActorUserId) : IRequest<FacultyOperationResult>;
