using BaseMVC.Domain.Common;

namespace BaseMVC.Domain.Faculties;

public sealed class Faculty : BaseAuditableEntity<int>
{
    public const int FacultyCodeLength = 2;
    public const int FacultyNameMaxLength = 200;

    public string FacultyCode { get; set; } = string.Empty;

    public string FacultyName { get; set; } = string.Empty;
}
