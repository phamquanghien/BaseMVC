using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaseMVC.Domain.Exams;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_standard_schedule_can_be_edited_without_changing_student_identity()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SVEDIT01");
        var exam = await CreateItExamAsync(factory, client, token);
        var headers = new[]
        {
            "STT", "StudentCode", "LastName", "FirstName", "ExamDate", "Session", "ExamDuration", "ExamRoom", "RetentionNote", "RetainedSection"
        };
        var workbook = BuildItWorkbook(headers,
            ["1", "SVEDIT01", "", "", "20/11/2026", "Ca 1", "9h30-11h30", "P.101", "", ""]);
        using var import = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", workbook);
        import.EnsureSuccessStatusCode();

        using var page = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        page.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var data = json.RootElement;
        Assert.Contains(data.GetProperty("editableFields").EnumerateArray(),
            field => field.GetProperty("key").GetString() == "examDate");
        var row = Assert.Single(data.GetProperty("items").EnumerateArray().ToArray());
        var recordId = row.GetProperty("id").GetString();
        Assert.NotNull(recordId);

        using var identityUpdate = new HttpRequestMessage(HttpMethod.Put, $"/Admin/Exams/{exam.Id}/data/{recordId}")
        {
            Content = JsonContent.Create(new { values = new { studentCode = "SV-OTHER" } })
        };
        identityUpdate.Headers.Add("RequestVerificationToken", token);
        using var rejectedIdentityUpdate = await client.SendAsync(identityUpdate);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedIdentityUpdate.StatusCode);

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/Admin/Exams/{exam.Id}/data/{recordId}")
        {
            Content = JsonContent.Create(new
            {
                values = new { examDate = "21/11/2026", session = "Ca 2", examRoom = "P.205" }
            })
        };
        updateRequest.Headers.Add("RequestVerificationToken", token);
        using var updated = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        using var refreshed = await client.GetAsync($"/Admin/Exams/{exam.Id}/data?kind=Candidates");
        refreshed.EnsureSuccessStatusCode();
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var updatedRow = Assert.Single(refreshedJson.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("21/11/2026", updatedRow.GetProperty("examDate").GetString());
        Assert.Equal("Ca 2", updatedRow.GetProperty("session").GetString());
        Assert.Equal("P.205", updatedRow.GetProperty("examRoom").GetString());
        Assert.Equal("SVEDIT01", updatedRow.GetProperty("studentCode").GetString());
    }
}
