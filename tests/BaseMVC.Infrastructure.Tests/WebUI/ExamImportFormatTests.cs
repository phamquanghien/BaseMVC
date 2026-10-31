using System.Net;
using System.Net.Http.Json;
using BaseMVC.Domain.Exams;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    private static readonly string[] StudentHeaders = ["STT", "StudentCode", "LastName", "FirstName"];

    public static TheoryData<string, string, int> ExamDataFormatColumns => new()
    {
        { "ItStandard", "Candidates", 10 },
        { "ItStandard", "Results", 8 },
        { "LanguageStandard", "Candidates", 13 },
        { "LanguageStandard", "Results", 11 },
        { "LanguagePlacement", "Candidates", 11 },
        { "LanguagePlacement", "Results", 11 },
        { "EnglishIntensive", "Results", 9 },
        { "ItBasicCertificate", "Candidates", 14 },
        { "ItBasicCertificate", "Results", 13 }
    };

    public static TheoryData<string, string> StudentLinkedFormats => new()
    {
        { "LanguageStandard", "Candidates" },
        { "LanguageStandard", "Results" },
        { "LanguagePlacement", "Candidates" },
        { "LanguagePlacement", "Results" },
        { "EnglishIntensive", "Results" }
    };

    [Theory]
    [MemberData(nameof(ExamDataFormatColumns))]
    public async Task Exam_import_templates_exist_for_every_format(string format, string kind, int columns)
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        var exam = await CreateExamWithFormatAsync(factory, client, token, Enum.Parse<ExamDataFormat>(format));

        using var response = await client.GetAsync($"/Admin/Exams/{exam.Id}/import/template?kind={kind}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheets.First();
        Assert.Equal("STT", sheet.Cell(1, 1).GetString());
        Assert.Equal(columns, sheet.LastColumnUsed()!.ColumnNumber());
    }

    [Fact]
    public async Task English_intensive_has_no_candidate_list()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.EnglishIntensive);

        using var template = await client.GetAsync($"/Admin/Exams/{exam.Id}/import/template?kind=Candidates");
        Assert.Equal(HttpStatusCode.BadRequest, template.StatusCode);
        using var data = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        Assert.Equal(HttpStatusCode.BadRequest, data.StatusCode);
        using var import = await PostExamImportAsync(
            client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(StudentHeaders, ["1", "SV01", "x", "y"]));
        Assert.Equal(HttpStatusCode.BadRequest, import.StatusCode);
        using var badKind = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Nope");
        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
    }

    [Fact]
    public async Task Exam_without_type_data_format_cannot_import_or_view_data()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, null);

        using var template = await client.GetAsync($"/Admin/Exams/{exam.Id}/import/template?kind=Results");
        Assert.Equal(HttpStatusCode.Conflict, template.StatusCode);
        using var data = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Results");
        Assert.Equal(HttpStatusCode.Conflict, data.StatusCode);
        using var import = await PostExamImportAsync(
            client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(StudentHeaders, ["1", "SV01"]));
        Assert.Equal(HttpStatusCode.Conflict, import.StatusCode);

        var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.Null(summary!.DataFormat);
        Assert.Empty(summary.Counts);
    }

    [Fact]
    public async Task Exam_type_data_format_can_be_set_validated_and_locked_after_data_import()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");

        using var invalid = await PostWithTokenAsync(client, "/Admin/ExamTypes", token, new { name = "Loại sai", dataFormat = "Nope" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var created = await PostWithTokenAsync(client, "/Admin/ExamTypes", token, new { name = "Loại có định dạng", dataFormat = "EnglishIntensive" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var examType = await created.Content.ReadFromJsonAsync<ExamTypeFormatDto>();
        Assert.Equal("EnglishIntensive", examType!.DataFormat);

        var exam = await CreateExamAsync(client, token, "Kỳ thi TAC", examType.Id);
        using var import = await PostExamImportAsync(
            client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(StudentHeaders, PadRow(9, "1", "SV01")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        using var locked = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{examType.Id}/edit", token, new { name = "Loại có định dạng", dataFormat = "ItStandard" });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        using var renamed = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{examType.Id}/edit", token, new { name = "Loại đổi tên", dataFormat = "EnglishIntensive" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
    }

    private sealed record ExamTypeFormatDto(int Id, string Name, string? DataFormat);

    [Theory]
    [MemberData(nameof(StudentLinkedFormats))]
    public async Task Student_linked_formats_import_with_student_names_and_status_changes(string format, string kind)
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01", "SV02");
        var exam = await CreateExamWithFormatAsync(factory, client, token, Enum.Parse<ExamDataFormat>(format));
        var columns = ExamDataFormatColumnCount(format, kind);

        var workbook = BuildItWorkbook(
            StudentHeaders,
            PadRow(columns, "1", "sv01", "Sai họ", "Sai tên"),
            PadRow(columns, "2", "SV02"),
            PadRow(columns, "3", "SV99"),
            PadRow(columns, "4", "SV01"));
        using var response = await PostExamImportAsync(client, token, exam.Id, kind, "ReplaceAll", workbook);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.Equal(4, result!.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal([4, 5], result.Errors.Select(error => error.Row).Order().ToArray());
        Assert.Contains("không tồn tại", result.Errors.Single(error => error.Row == 4).Message);

        var list = await client.GetFromJsonAsync<PagedResponse<CandidateRow>>($"/Admin/Exams/{exam.Id}/data?kind={kind}");
        Assert.Equal(["Họ", "Trần"], list!.Items.Select(item => item.LastName).ToArray());
        Assert.Equal(2, list.TotalCount);
        Assert.DoesNotContain(list.Items, item => item.LastName == "Sai họ");

        var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.Equal(kind == "Candidates" ? "ScheduleArranged" : "ResultsEntered", summary!.Status);
        Assert.Equal(2, summary.Counts.Single(count => count.Format == format && count.Kind == kind).Count);

        // Other formats of the same exam stay empty.
        Assert.Equal(2, summary.Counts.Sum(count => count.Count));

        using var delete = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Language_standard_candidates_read_exam_and_speaking_dates()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01", "SV02");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.LanguageStandard);

        var workbook = BuildItWorkbook(
            StudentHeaders,
            ["1", "SV01", "", "", "Kỳ 1", "Nghe", "ghi chú", "20/11/2026", "Ca 1", "P.1", "21/11/2026", "Ca 2", "P.2"],
            ["2", "SV02", "", "", "", "", "", "20/11/2026", "Ca 1", "P.1", "21-11-2026", "", ""]);
        using var response = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        var result = await response.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.Equal(1, result!.ImportedCount);
        Assert.Contains("ExamDateSpeaking", result.Errors.Single().Message);

        var page = await client.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(
            $"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        var item = page!["items"][0];
        Assert.Equal("20/11/2026", item.GetProperty("examDate").GetString());
        Assert.Equal("21/11/2026", item.GetProperty("examDateSpeaking").GetString());
        Assert.Equal("P.2", item.GetProperty("examRoomSpeaking").GetString());
        Assert.Equal("Kỳ 1", item.GetProperty("retentionExam").GetString());
    }

    [Fact]
    public async Task It_basic_certificate_imports_independent_candidates_and_rejects_duplicates()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.ItBasicCertificate);
        string[] headers = ["STT"];

        var workbook = BuildItWorkbook(
            headers,
            ["1", "001", "SBD1", "Nguyễn Văn", "An", "01/02/2005", "Nam", "Kinh", "Hà Nội", "", "20/11/2026", "Ca 1", "09h00-11h00", "P.1"],
            ["2", "002", "SBD2", "Trần", "Bình", "15/03/2004", "Nữ", "", "", "ghi chú", "", "", "", ""],
            ["3", "001", "SBD3", "Trùng", "CCCD", "01/01/2000", "Nam", "", "", "", "", "", "", ""],
            ["4", "004", "sbd1", "Trùng", "SBD", "01/01/2000", "Nam", "", "", "", "", "", "", ""],
            ["5", "005", "SBD5", "Sai", "Giới tính", "01/01/2000", "Khác", "", "", "", "", "", "", ""],
            ["6", "006", "SBD6", "Thiếu", "Ngày sinh", "", "Nam", "", "", "", "", "", "", ""],
            ["7", "", "SBD7", "Thiếu", "CCCD", "01/01/2000", "Nam", "", "", "", "", "", "", ""]);
        using var response = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.Equal(7, result!.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Contains("Số CCCD \"001\"", result.Errors.Single(error => error.Row == 4).Message);
        Assert.Contains("Số báo danh \"sbd1\"", result.Errors.Single(error => error.Row == 5).Message);
        Assert.Contains("Gender", result.Errors.Single(error => error.Row == 6).Message);
        Assert.Contains("DateOfBirth", result.Errors.Single(error => error.Row == 7).Message);
        Assert.Contains("IdentityNumber", result.Errors.Single(error => error.Row == 8).Message);

        var page = await client.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(
            $"/Admin/Exams/{exam.Id}/data?kind=Candidates&search=tr%E1%BA%A7n");
        Assert.Equal(1, page!["totalCount"].GetInt32());
        var item = page["items"][0];
        Assert.Equal("15/03/2004", item.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Nữ", item.GetProperty("gender").GetString());
        Assert.Equal("SBD2", item.GetProperty("candidateNumber").GetString());

        // AddOnly keeps existing rows and treats clashes with them as errors.
        var addOnly = BuildItWorkbook(
            headers,
            ["1", "002", "SBD9", "Trùng", "Cũ", "01/01/2000", "Nam", "", "", "", "", "", "", ""],
            ["2", "009", "SBD9", "Mới", "Hoàn toàn", "01/01/2000", "Nam", "", "", "", "", "", "", ""]);
        await SetExamStatusAsync(factory, exam.Id, ExamStatus.AwaitingSchedule);
        using var added = await PostExamImportAsync(client, token, exam.Id, "Candidates", "AddOnly", addOnly);
        var addedResult = await added.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.Equal(1, addedResult!.ImportedCount);
        var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.Equal(3, summary!.Counts.Single(count => count.Format == "ItBasicCertificate" && count.Kind == "Candidates").Count);

        // Results use their own column order (candidate number before ID number).
        var results = BuildItWorkbook(
            headers,
            ["1", "SBD1", "001", "Nguyễn Văn", "An", "01/02/2005", "Nam", "Kinh", "Hà Nội", "18", "70", "Đạt", ""],
            ["2", "SBD2", "002", "Trần", "Bình", "15/03/2004", "Nữ", "", "", "10", "20", "Không đạt", "n"]);
        using var resultResponse = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", results);
        var resultOutcome = await resultResponse.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.Equal(2, resultOutcome!.ImportedCount);
        var resultPage = await client.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(
            $"/Admin/Exams/{exam.Id}/data?kind=Results&search=SBD2");
        Assert.Equal("Không đạt", resultPage!["items"][0].GetProperty("result").GetString());
        Assert.Equal("002", resultPage["items"][0].GetProperty("identityNumber").GetString());

        var final = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.Equal(nameof(ExamStatus.ResultsEntered), final!.Status);

        using var delete = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Student_used_only_in_new_formats_cannot_be_deleted()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.EnglishIntensive);

        using var import = await PostExamImportAsync(
            client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(StudentHeaders, PadRow(9, "1", "SV01")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        int studentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            studentId = await scope.ServiceProvider.GetRequiredService<BaseMVC.Infrastructure.Persistence.ApplicationDbContext>()
                .Students.Select(student => student.Id).SingleAsync();
        }

        using var delete = await PostWithTokenAsync(client, $"/Admin/Students/{studentId}/delete", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    private static int ExamDataFormatColumnCount(string format, string kind) => (format, kind) switch
    {
        ("LanguageStandard", "Candidates") => 13,
        ("LanguagePlacement", "Candidates") => 11,
        ("EnglishIntensive", _) => 9,
        _ => 11
    };

    private static string[] PadRow(int columns, params string[] values)
    {
        var row = new string[columns];
        Array.Fill(row, string.Empty);
        for (var index = 0; index < values.Length; index++) row[index] = values[index];
        return row;
    }
}
