namespace BaseMVC.Domain.Exams;

/// <summary>Danh sách thi Chứng chỉ ứng dụng CNTT cơ bản. Thí sinh thi độc lập, không liên kết bảng Student.</summary>
public sealed class ItBasicCertificateCandidate : ExamRecord
{
    public string IdentityNumber { get; set; } = string.Empty;

    public string CandidateNumber { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public bool Gender { get; set; }

    public string? Ethnicity { get; set; }

    public string? BirthProvince { get; set; }

    public string? Note { get; set; }

    public DateOnly? ExamDate { get; set; }

    public string? Session { get; set; }

    public string? ExamDuration { get; set; }

    public string? ExamRoom { get; set; }
}

/// <summary>Kết quả thi Chứng chỉ ứng dụng CNTT cơ bản.</summary>
public sealed class ItBasicCertificateResult : ExamRecord
{
    public string IdentityNumber { get; set; } = string.Empty;

    public string CandidateNumber { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public bool Gender { get; set; }

    public string? Ethnicity { get; set; }

    public string? BirthProvince { get; set; }

    public string? MultipleChoiceScore { get; set; }

    public string? PracticalScore { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }
}
