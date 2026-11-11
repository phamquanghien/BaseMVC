using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_mock_online_schedule_can_be_edited()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "ITMOCK01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.ItMockOnline);
        var headers = new[]
        {
            "STT", "Mã sinh viên", "Họ đệm", "Tên", "Tên phòng thi trên Microsoft Teams", "Ngày thi thử",
            "Thời gian bắt đầu đăng nhập Microsoft Teams trên thiết bị 1",
            "Thời gian bắt đầu đăng nhập HUMG e-Exams trên thiết bị 2", "Ghi chú/Đợt BL", "Phần bảo lưu"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "ITMOCK01", "", "", "Teams-01", "20/11/2026", "08:00", "08:15", "Đợt 1", "Thực hành"]);
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
                values = new { mockExamDate = "22/11/2026", microsoftTeamsRoomName = "Teams-Updated", teamsDeviceLoginStartTime = "09:00" }
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
        Assert.Equal("Teams-Updated", updatedRow.GetProperty("microsoftTeamsRoomName").GetString());
        Assert.Equal("09:00", updatedRow.GetProperty("teamsDeviceLoginStartTime").GetString());
        Assert.Equal("ITMOCK01", updatedRow.GetProperty("studentCode").GetString());
    }
}
