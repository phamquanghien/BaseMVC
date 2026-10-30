using BaseMVC.Domain.Common;

namespace BaseMVC.Domain.Students;

public sealed class Student : BaseAuditableEntity<int>
{
    public const int StudentCodeMaxLength = 12;
    public const int NameMaxLength = 100;
    public const int ClassCodeMaxLength = 200;
    public const int FacultyCodeLength = 2;

    public string StudentCode { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string ClassCode { get; set; } = string.Empty;

    public string FacultyCode { get; set; } = string.Empty;

    public bool Gender { get; set; }

    public bool EligibleForEnglish { get; set; }

    public bool PassedLanguageRequirement { get; set; }

    public bool PassedITRequirement { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
