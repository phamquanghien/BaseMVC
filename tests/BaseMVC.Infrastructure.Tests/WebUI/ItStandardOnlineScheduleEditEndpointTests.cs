using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_standard_online_schedule_can_be_edited()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "ITSTDON01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.ItStandardOnline);
        var headers = new[]
        {
            "Số thứ tự", "Mã sinh viên", "Họ đệm", "Tên", "Ca thi",
            "Thời gian bắt đầu đăng nhập Microsoft Teams", "Thời gian bắt đầu đăng nhập HUMG e-Exams",
            "Tên phòng thi trên Microsoft Teams", "Ghi chú/Đợt BL", "Phần bảo lưu"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "ITSTDON01", "", "", "Ca 1", "08:00", "08:15", "HUMG-IT-01", "Đợt 1", "Thực hành"]);
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
                    session = "Ca 2", teamsLoginStartTime = "09:00", eExamsLoginStartTime = "09:15",
                    microsoftTeamsRoomName = "HUMG-IT-Updated", note = "Đợt 2"
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
        Assert.Equal("Ca 2", updatedRow.GetProperty("session").GetString());
        Assert.Equal("09:00", updatedRow.GetProperty("teamsLoginStartTime").GetString());
        Assert.Equal("09:15", updatedRow.GetProperty("eExamsLoginStartTime").GetString());
        Assert.Equal("HUMG-IT-Updated", updatedRow.GetProperty("microsoftTeamsRoomName").GetString());
        Assert.Equal("Đợt 2", updatedRow.GetProperty("note").GetString());
        Assert.Equal("ITSTDON01", updatedRow.GetProperty("studentCode").GetString());
    }
}
