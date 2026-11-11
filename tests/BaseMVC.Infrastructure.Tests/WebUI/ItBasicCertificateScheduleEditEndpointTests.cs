using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_basic_certificate_schedule_can_be_edited_without_changing_candidate_identity()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.ItBasicCertificate);
        var headers = new[]
        {
            "STT", "IdentityNumber", "CandidateNumber", "LastName", "FirstName", "DateOfBirth", "Gender",
            "Ethnicity", "BirthProvince", "Note", "ExamDate", "Session", "ExamDuration", "ExamRoom"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "001234567890", "CC0001", "Nguyễn", "An", "01/01/2000", "Nam", "Kinh", "Hà Nội", "Lịch thi", "20/11/2026", "Ca 1", "09h00-11h00", "P.101"]);
        using var import = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        import.EnsureSuccessStatusCode();

        using var page = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates&search=001234567890");
        page.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var fields = json.RootElement.GetProperty("editableFields").EnumerateArray().ToArray();
        Assert.Contains(fields, field => field.GetProperty("key").GetString() == "examDate");
        Assert.DoesNotContain(fields, field => field.GetProperty("key").GetString() == "identityNumber");
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToArray());
        var recordId = row.GetProperty("id").GetString();

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/Admin/Exams/{exam.Id}/data/{recordId}")
        {
            Content = JsonContent.Create(new
            {
                values = new { examDate = "22/11/2026", session = "Ca 2", examRoom = "P.205", note = "Đã đổi lịch" }
            })
        };
        update.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var refreshed = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates&search=001234567890");
        refreshed.EnsureSuccessStatusCode();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var updatedRow = Assert.Single(refreshedJson.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("22/11/2026", updatedRow.GetProperty("examDate").GetString());
        Assert.Equal("Ca 2", updatedRow.GetProperty("session").GetString());
        Assert.Equal("P.205", updatedRow.GetProperty("examRoom").GetString());
        Assert.Equal("Đã đổi lịch", updatedRow.GetProperty("note").GetString());
        Assert.Equal("001234567890", updatedRow.GetProperty("identityNumber").GetString());
        Assert.Equal("CC0001", updatedRow.GetProperty("candidateNumber").GetString());
    }
}
