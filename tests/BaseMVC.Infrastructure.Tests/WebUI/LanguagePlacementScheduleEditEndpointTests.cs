using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Language_placement_schedule_can_be_edited()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "PLACEDIT01");
        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.LanguagePlacement);
        var headers = new[]
        {
            "STT", "StudentCode", "LastName", "FirstName", "Note", "ExamDate", "Session", "ExamRoom",
            "ExamDateSpeaking", "SessionSpeaking", "ExamRoomSpeaking"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "PLACEDIT01", "", "", "", "20/11/2026", "Ca 1", "P.101", "21/11/2026", "Ca 1", "P.102"]);
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
                values = new { examDate = "22/11/2026", examRoomSpeaking = "P.205", sessionSpeaking = "Ca 3" }
            })
        };
        update.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var refreshed = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        refreshed.EnsureSuccessStatusCode();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var updatedRow = Assert.Single(refreshedJson.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("22/11/2026", updatedRow.GetProperty("examDate").GetString());
        Assert.Equal("P.205", updatedRow.GetProperty("examRoomSpeaking").GetString());
        Assert.Equal("Ca 3", updatedRow.GetProperty("sessionSpeaking").GetString());
        Assert.Equal("PLACEDIT01", updatedRow.GetProperty("studentCode").GetString());
    }
}
