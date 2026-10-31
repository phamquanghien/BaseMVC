namespace BaseMVC.Domain.Exams;

/// <summary>Định dạng dữ liệu (danh sách thi/kết quả thi) của một loại kỳ thi. Mỗi định dạng có bảng dữ liệu và file Excel riêng.</summary>
public enum ExamDataFormat
{
    ItStandard,
    LanguageStandard,
    LanguagePlacement,
    EnglishIntensive,
    ItBasicCertificate
}
