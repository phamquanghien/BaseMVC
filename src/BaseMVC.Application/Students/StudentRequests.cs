using MediatR;

namespace BaseMVC.Application.Students;

public sealed record SearchStudentsQuery(string? Search, int Page, int PageSize) : IRequest<StudentSearchResponse>;
public sealed record ListFacultyOptionsQuery : IRequest<IReadOnlyList<FacultyOption>>;
public sealed record CreateStudentCommand(StudentInput Input, string? ActorUserId) : IRequest<StudentOperationResult>;
public sealed record EditStudentCommand(int Id, StudentInput Input, string? ActorUserId) : IRequest<StudentOperationResult>;
public sealed record DeleteStudentCommand(int Id, string? ActorUserId) : IRequest<StudentOperationResult>;
public sealed record GetStudentImportTemplateQuery : IRequest<byte[]>;

public sealed record ImportStudentsCommand(Stream Content, string? ActorUserId) : IRequest<StudentImportOutcome>;
