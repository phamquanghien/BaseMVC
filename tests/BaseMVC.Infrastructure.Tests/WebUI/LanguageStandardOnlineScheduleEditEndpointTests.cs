using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Language_standard_online_schedule_preserves_freeform_exam_dates_when_edited()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "LANGSTDON1");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.LanguageStandardOnline);
        var headers = new[]
        {
            "Số thứ tự", "Mã sinh viên", "Họ đệm", "Tên", "Phần bảo lưu", "Đợt bảo lưu", "Ghi chú",
            "Ngày thi Nghe Đọc Viết", "Phòng thi Nghe Đọc Viết trên Microsoft TEAMS",
            "Thời gian đăng nhập Microsoft TEAMS", "Thời gian bắt đầu làm bài trên HUMG e-Exams",
            "Ngày thi Nói", "Phòng thi Nói Microsoft TEAMS", "Thời gian đăng nhập Microsoft TEAMS"
        };
        var workbook = BuildItWorkbook(headers,
        [
            "1", "LANGSTDON1", "", "", "Nghe", "Đợt 1", "Ghi chú", "Ca 1, ngày 08/06/2026",
            "Teams-LRW", "08:00", "08:30", "Ca 1, ngày 09/06/2026", "Teams-Speaking", "09:00"
        ]);
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
                values = new
                {
                    examDate = "Ca 2, ngày 10/06/2026", examRoom = "Teams-LRW-Updated",
                    examDateSpeaking = "Ca 3, ngày 11/06/2026", examRoomSpeaking = "Teams-Speaking-Updated",
                    teamsLoginStartTimeSpeaking = "10:00"
                }
            })
        };
        update.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var refreshed = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        refreshed.EnsureSuccessStatusCode();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var updatedRow = Assert.Single(refreshedJson.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("Ca 2, ngày 10/06/2026", updatedRow.GetProperty("examDate").GetString());
        Assert.Equal("Teams-LRW-Updated", updatedRow.GetProperty("examRoom").GetString());
        Assert.Equal("Ca 3, ngày 11/06/2026", updatedRow.GetProperty("examDateSpeaking").GetString());
        Assert.Equal("Teams-Speaking-Updated", updatedRow.GetProperty("examRoomSpeaking").GetString());
        Assert.Equal("10:00", updatedRow.GetProperty("teamsLoginStartTimeSpeaking").GetString());
        Assert.Equal("LANGSTDON1", updatedRow.GetProperty("studentCode").GetString());
    }
}
