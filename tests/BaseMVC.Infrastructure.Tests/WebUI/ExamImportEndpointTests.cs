using System.Net;
using System.Net.Http.Json;
using BaseMVC.Domain.Exams;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    private static readonly string[] CandidateHeaders =
        ["STT", "StudentCode", "LastName", "FirstName", "ExamDate", "Session", "ExamDuration", "ExamRoom", "RetentionNote", "RetainedSection"];

    private static readonly string[] ResultHeaders =
        ["STT", "StudentCode", "LastName", "FirstName", "MultipleChoiceScore", "PracticalScore", "FinalScore", "Note"];

    [Fact]
    public async Task Exam_import_endpoints_enforce_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "exam-import-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "ExamImportLimited");
        await factory.SetRolePermissionAsync("ExamImportLimited", "Permissions.Exams.Read", grant: true);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousPage = await anonymous.GetAsync("/Admin/Exams/1");
        Assert.NotEqual(HttpStatusCode.OK, anonymousPage.StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        using var page = await client.GetAsync("/Admin/Exams");
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());

        using var details = await client.GetAsync("/Admin/Exams/1");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var summary = await client.GetAsync("/Admin/Exams/1/summary");
        Assert.Equal(HttpStatusCode.NotFound, summary.StatusCode);
        using var template = await client.GetAsync("/Admin/Exams/import/template?format=ItStandard&kind=Candidates");
        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
        using var import = await PostExamImportAsync(client, token, 1, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders));
        Assert.Equal(HttpStatusCode.Forbidden, import.StatusCode);
        using var status = await PostWithTokenAsync(client, "/Admin/Exams/1/status", token, new { status = "Taken" });
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task Exam_status_change_requires_its_own_permission()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "exam-status-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "ExamStatusLimited");
        foreach (var permission in new[] { "Read", "Create", "Update", "Delete" })
            await factory.SetRolePermissionAsync("ExamStatusLimited", $"Permissions.Exams.{permission}", grant: true);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        using var page = await client.GetAsync("/Admin/Exams");
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());
        var exam = await CreateItExamAsync(factory, client, token);

        using var denied = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/status", token, new { status = "Taken" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await factory.SetRolePermissionAsync("ExamStatusLimited", "Permissions.Exams.ChangeStatus", grant: true);
        using var allowed = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/status", token, new { status = "Taken" });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Exam_import_templates_are_downloadable()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);

        foreach (var (kind, headers) in new[] { ("Candidates", CandidateHeaders), ("Results", ResultHeaders) })
        {
            using var response = await client.GetAsync($"/Admin/Exams/import/template?format=ItStandard&kind={kind}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
            var sheet = workbook.Worksheets.First();
            for (var column = 0; column < headers.Length; column++)
                Assert.Equal(headers[column], sheet.Cell(1, column + 1).GetString());
        }

        using var invalid = await client.GetAsync("/Admin/Exams/import/template?format=ItStandard&kind=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Exam_candidates_import_validates_rows_and_schedules_exam()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01", "SV02", "SV03", "SV04");
        var exam = await CreateItExamAsync(factory, client, token);

        using var missingToken = await client.PostAsync($"/Admin/Exams/{exam.Id}/import", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using var notExcel = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", [1, 2, 3], "x.txt");
        Assert.Equal(HttpStatusCode.BadRequest, notExcel.StatusCode);
        using var empty = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var badMode = await PostExamImportAsync(client, token, exam.Id, "Candidates", "Nope", BuildItWorkbook(CandidateHeaders, ["1", "SV01", "A", "B"]));
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);
        using var missingExam = await PostExamImportAsync(client, token, 9999, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders, ["1", "SV01", "A", "B"]));
        Assert.Equal(HttpStatusCode.NotFound, missingExam.StatusCode);

        var workbook = BuildItWorkbook(
            CandidateHeaders,
            ["1", "SV01", "Nguyễn Văn", "An", "20/11/2026", "Ca 1", "9h30-11h30", "P.101", "Kỳ 1", "Lý thuyết"],
            ["2", "SV02", "Trần", "Bình", "05/12/26", "Ca 2", "", "", "", ""],
            ["3", "SV99", "Không", "Tồn tại", "", "", "", "", "", ""],
            ["4", "SV01", "Trùng", "Trong file", "", "", "", "", "", ""],
            ["x", "SV03", "STT bị bỏ qua", "", "", "", "", "", "", ""],
            ["5", "SV04", "Sai", "Ngày", "2026-11-20", "", "", "", "", ""],
            ["", "", "", "", "", "", "", "", "", ""],
            ["6", "SV03", "", "Thiếu họ", "", "", "", "", "", ""]);
        using var response = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(7, result.TotalRows);
        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(new[] { 4, 5, 7, 9 }, result.Errors.Select(error => error.Row).Order().ToArray());

        var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.NotNull(summary);
        Assert.Equal("ScheduleArranged", summary.Status);
        Assert.Equal(3, summary.ItCandidateCount);

        var list = await client.GetFromJsonAsync<PagedResponse<CandidateRow>>($"/Admin/Exams/{exam.Id}/it-candidates?page=1&pageSize=10");
        Assert.NotNull(list);
        Assert.Equal("20/11/2026", list.Items[0].ExamDate);
        // Names in the file ("Nguyễn Văn", "An") are ignored: they always come from the Student table.
        Assert.Equal("Họ", list.Items[0].LastName);
        Assert.Equal("Tên", list.Items[0].FirstName);
        Assert.Equal("05/12/2026", list.Items[1].ExamDate);
        var filtered = await client.GetFromJsonAsync<PagedResponse<CandidateRow>>($"/Admin/Exams/{exam.Id}/it-candidates?search=TR%E1%BA%A6N");
        Assert.NotNull(filtered);
        Assert.Equal("SV02", Assert.Single(filtered.Items).StudentCode);

        using var blockedAfterSchedule = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders, ["1", "SV03", "Lê", "Cường"]));
        Assert.Equal(HttpStatusCode.Conflict, blockedAfterSchedule.StatusCode);

        await SetExamStatusAsync(factory, exam.Id, ExamStatus.AwaitingSchedule);
        using var again = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders, ["1", "SV03", "Lê", "Cường"]));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var replaced = await client.GetFromJsonAsync<PagedResponse<CandidateRow>>($"/Admin/Exams/{exam.Id}/it-candidates");
        Assert.Equal("SV03", Assert.Single(replaced!.Items).StudentCode);

        foreach (var status in new[] { ExamStatus.RegistrationOpen, ExamStatus.RegistrationCompleted, ExamStatus.AwaitingSchedule })
        {
            await SetExamStatusAsync(factory, exam.Id, status);
            using var allowed = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders, ["1", "SV04", "Phạm", "Dũng"]));
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            var current = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
            Assert.Equal("ScheduleArranged", current!.Status);
        }

        foreach (var status in new[] { ExamStatus.ScheduleArranged, ExamStatus.Taken })
        {
            await SetExamStatusAsync(factory, exam.Id, status);
            using var blocked = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", BuildItWorkbook(CandidateHeaders, ["1", "SV01", "Lê", "Cường"]));
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }
    }

    [Fact]
    public async Task Exam_results_import_supports_replace_and_add_only_modes()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01", "SV02", "SV03");
        var exam = await CreateItExamAsync(factory, client, token);
        await SetExamStatusAsync(factory, exam.Id, ExamStatus.Taken);

        var first = BuildItWorkbook(
            ResultHeaders,
            ["1", "SV01", "Nguyễn", "An", "7,5", "8", "75.5", "Đạt"],
            ["2", "SV02", "Trần", "Bình", "5", "5", "101", ""],
            ["3", "SV03", "Lê", "Cường", "", "", "abc", ""],
            ["4", "SV03", "Lê", "Cường", "", "", "", "Vắng"]);
        using var firstResponse = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.NotNull(firstResult);
        Assert.Equal(2, firstResult.ImportedCount);
        Assert.Equal(2, firstResult.Errors.Count);

        var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
        Assert.NotNull(summary);
        Assert.Equal("ResultsEntered", summary.Status);

        await SetExamStatusAsync(factory, exam.Id, ExamStatus.Taken);
        using var addOnly = await PostExamImportAsync(client, token, exam.Id, "Results", "AddOnly", BuildItWorkbook(
            ResultHeaders,
            ["1", "SV01", "Nguyễn", "An", "1", "1", "10", "Trùng"],
            ["2", "SV02", "Trần", "Bình", "6", "6", "60", "Mới"]));
        var addOnlyResult = await addOnly.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.NotNull(addOnlyResult);
        Assert.Equal(1, addOnlyResult.ImportedCount);
        Assert.Single(addOnlyResult.Errors);

        var all = await client.GetFromJsonAsync<PagedResponse<ResultRow>>($"/Admin/Exams/{exam.Id}/it-results?page=1&pageSize=10");
        Assert.NotNull(all);
        Assert.Equal(3, all.TotalCount);
        var first01 = all.Items.Single(item => item.StudentCode == "SV01");
        Assert.Equal("75.5", first01.FinalScore);
        Assert.Equal("Họ", first01.LastName);
        Assert.Equal("Tên", first01.FirstName);

        await SetExamStatusAsync(factory, exam.Id, ExamStatus.Taken);
        using var badOnly = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(
            ResultHeaders, ["1", "NOPE", "A", "B"]));
        var badOnlyResult = await badOnly.Content.ReadFromJsonAsync<ExamImportResultDto>();
        Assert.NotNull(badOnlyResult);
        Assert.Equal(0, badOnlyResult.ImportedCount);
        var unchanged = await client.GetFromJsonAsync<PagedResponse<ResultRow>>($"/Admin/Exams/{exam.Id}/it-results");
        Assert.Equal(3, unchanged!.TotalCount);

        await SetExamStatusAsync(factory, exam.Id, ExamStatus.Taken);
        using var replace = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(
            ResultHeaders, ["1", "SV03", "Lê", "Cường", "9", "9", "90", ""]));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var replaced = await client.GetFromJsonAsync<PagedResponse<ResultRow>>($"/Admin/Exams/{exam.Id}/it-results");
        Assert.Equal("SV03", Assert.Single(replaced!.Items).StudentCode);

        foreach (var status in new[] { ExamStatus.ResultsEntered, ExamStatus.Completed })
        {
            await SetExamStatusAsync(factory, exam.Id, status);
            using var blocked = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(
                ResultHeaders, ["1", "SV01", "Nguyễn", "An"]));
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }

        await SetExamStatusAsync(factory, exam.Id, ExamStatus.NotStarted);
        using var early = await PostExamImportAsync(client, token, exam.Id, "Results", "AddOnly", BuildItWorkbook(
            ResultHeaders, ["1", "SV01", "Nguyễn", "An", "5", "5", "50", ""]));
        Assert.Equal(HttpStatusCode.OK, early.StatusCode);
    }

    [Fact]
    public async Task Exam_with_imported_results_is_soft_deleted()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");
        var exam = await CreateItExamAsync(factory, client, token);

        using var import = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(
            ResultHeaders, ["1", "SV01", "Nguyễn", "An", "5", "5", "50", ""]));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        using var delete = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var hidden = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search");
        Assert.Empty(hidden!.Items);
        var shown = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search?includeDeleted=true");
        Assert.Single(shown!.Items);
        using var details = await client.GetAsync($"/Admin/Exams/{exam.Id}/it-results");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }

    [Fact]
    public async Task Exam_status_can_be_changed_and_student_with_exam_data_cannot_be_deleted()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV01");
        var exam = await CreateItExamAsync(factory, client, token);

        using var results = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", BuildItWorkbook(
            ResultHeaders, ["1", "SV01", "x", "y", "5", "5", "50", ""]));
        Assert.Equal(HttpStatusCode.OK, results.StatusCode);

        foreach (var status in new[] { "Completed", "NotStarted", "AwaitingSchedule" })
        {
            using var changed = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/status", token, new { status });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var summary = await client.GetFromJsonAsync<ExamSummaryResponse>($"/Admin/Exams/{exam.Id}/summary");
            Assert.Equal(status, summary!.Status);
        }

        using var invalid = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/status", token, new { status = "Nope" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await PostWithTokenAsync(client, "/Admin/Exams/9999/status", token, new { status = "Taken" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var noToken = await client.PostAsJsonAsync($"/Admin/Exams/{exam.Id}/status", new { status = "Taken" });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        int studentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            studentId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Students.Select(student => student.Id).SingleAsync();
        }

        using var delete = await PostWithTokenAsync(client, $"/Admin/Students/{studentId}/delete", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    private static async Task SeedStudentsAsync(UserManagementWebApplicationFactory factory, params string[] codes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Faculties.Add(new BaseMVC.Domain.Faculties.Faculty
        {
            FacultyCode = "CT", FacultyName = "Công nghệ", CreatedAtUtc = DateTimeOffset.UtcNow
        });
        foreach (var code in codes)
        {
            dbContext.Students.Add(new Student
            {
                StudentCode = code, LastName = code == "SV02" ? "Trần" : "Họ", FirstName = "Tên", ClassCode = "CT01", FacultyCode = "CT",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<ExamDto> CreateItExamAsync(
        UserManagementWebApplicationFactory factory, HttpClient client, string token)
    {
        var types = await SeedExamTypesAsync(factory, client);
        return await CreateExamAsync(client, token, "Chuẩn đầu ra CNTT", types[0].Id);
    }

    private static byte[] BuildItWorkbook(string[] headers, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).SetValue(headers[column]);
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < rows[row].Length; column++)
            sheet.Cell(row + 2, column + 1).SetValue(rows[row][column]);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<HttpResponseMessage> PostExamImportAsync(
        HttpClient client, string token, int examId, string kind, string mode, byte[] content, string fileName = "data.xlsx")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", fileName);
        form.Add(new StringContent("ItStandard"), "format");
        form.Add(new StringContent(kind), "kind");
        form.Add(new StringContent(mode), "mode");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Exams/{examId}/import") { Content = form };
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private sealed record ExamImportResultDto(int TotalRows, int ImportedCount, List<ExamImportErrorDto> Errors);

    private sealed record ExamImportErrorDto(int Row, string Message);

    private sealed record ExamSummaryResponse(int Id, string Name, string Status, int ItCandidateCount, int ItResultCount);

    private sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    private sealed record CandidateRow(string StudentCode, string LastName, string FirstName, string? ExamDate);

    private sealed record ResultRow(string StudentCode, string LastName, string FirstName, string? FinalScore);
}
