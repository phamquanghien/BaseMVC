using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Language_mock_online_schedule_can_be_edited()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "LANGMOCK01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.LanguageMockOnline);
        var headers = new[]
        {
            "STT", "Mã sinh viên", "Họ đệm", "Tên", "Ngày thi thử", "Phòng thi thử",
            "Thời gian đăng nhập Ms TEAMS", "Thời gian đăng nhập SEB", "Phần bảo lưu", "Đợt bảo lưu"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "LANGMOCK01", "", "", "20/11/2026", "P.101", "08:00", "08:15", "Nghe", "Đợt 1"]);
        using var import = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        import.EnsureSuccessStatusCode();

        using var page = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        page.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToArray());
        var recordId = row.GetProperty("id").GetString();

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/Admin/Exams/{exam.Id}/data/{recordId}")
        {
            Content = JsonContent.Create(new
            {
                values = new { mockExamDate = "22/11/2026", mockExamRoom = "P.205", sebLoginStartTime = "09:15", retentionBatch = "Đợt 2" }
            })
        };
        update.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var refreshed = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        refreshed.EnsureSuccessStatusCode();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var updatedRow = Assert.Single(refreshedJson.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("22/11/2026", updatedRow.GetProperty("mockExamDate").GetString());
        Assert.Equal("P.205", updatedRow.GetProperty("mockExamRoom").GetString());
        Assert.Equal("09:15", updatedRow.GetProperty("sebLoginStartTime").GetString());
        Assert.Equal("Đợt 2", updatedRow.GetProperty("retentionBatch").GetString());
        Assert.Equal("LANGMOCK01", updatedRow.GetProperty("studentCode").GetString());
    }
}
