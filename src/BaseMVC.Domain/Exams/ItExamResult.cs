namespace BaseMVC.Domain.Exams;

/// <summary>Kết quả thi của kỳ thi Chuẩn đầu ra CNTT. Điểm lưu dạng chuỗi theo chuẩn của file Excel.</summary>
public sealed class ItExamResult : StudentExamRecord
{
    public const int ScoreMaxLength = 50;
    public const int NoteMaxLength = 1000;

    public string? MultipleChoiceScore { get; set; }

    public string? PracticalScore { get; set; }

    public string? FinalScore { get; set; }

    public string? Note { get; set; }
}
