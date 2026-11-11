using BaseMVC.Application.Exams;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_standard_online_imports_separate_schedule_and_standard_results_and_supports_lookup()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);
        await SeedStudentsAsync(factory, "SV-ONLINE-01");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var student = await db.Students.SingleAsync(item => item.StudentCode == "SV-ONLINE-01");
            student.DateOfBirth = new DateOnly(2005, 12, 1);
            await db.SaveChangesAsync();
        }

        var exam = await CreateExamWithFormatAsync(factory, client, token, ExamDataFormat.ItStandardOnline);
        var scheduleHeaders = new[]
        {
            "Số thứ tự", "Mã sinh viên", "Họ đệm", "Tên", "Ca thi",
            "Thời gian bắt đầu đăng nhập Microsoft Teams", "Thời gian bắt đầu đăng nhập HUMG e-Exams",
            "Tên phòng thi trên Microsoft Teams", "Ghi chú/Đợt BL", "Phần bảo lưu"
        };
        var scheduleWorkbook = BuildItWorkbook(scheduleHeaders,
            ["1", "SV-ONLINE-01", "Tên trong Excel", "Sinh viên", "Ca 1", "08:00", "08:15", "HUMG-IT-01", "Đợt 1", "Thực hành"]);
        using var scheduleImport = await PostExamImportAsync(client, token, exam.Id, "Candidates", "ReplaceAll", scheduleWorkbook);
        scheduleImport.EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var schedule = await db.ItStandardOnlineSchedules.SingleAsync();
            Assert.Equal("Ca 1", schedule.Session);
            Assert.Equal("08:00", schedule.TeamsLoginStartTime);
            Assert.Equal("08:15", schedule.EExamsLoginStartTime);
            Assert.Equal("HUMG-IT-01", schedule.MicrosoftTeamsRoomName);
            Assert.Equal("Đợt 1", schedule.Note);
            Assert.Equal("Thực hành", schedule.RetainedSection);
            Assert.Equal("Họ", (await db.Students.SingleAsync(item => item.Id == schedule.StudentId)).LastName);
        }

        var resultHeaders = new[]
        {
            "STT", "StudentCode", "LastName", "FirstName", "MultipleChoiceScore", "PracticalScore", "FinalScore", "Note"
        };
        var resultWorkbook = BuildItWorkbook(resultHeaders,
            ["1", "SV-ONLINE-01", "", "", "7.5", "8", "75", "Đạt"]);
        using var resultImport = await PostExamImportAsync(client, token, exam.Id, "Results", "ReplaceAll", resultWorkbook);
        resultImport.EnsureSuccessStatusCode();

        await using var lookupScope = factory.Services.CreateAsyncScope();
        var lookup = lookupScope.ServiceProvider.GetRequiredService<IPublicExamLookupService>();
        var types = await lookup.ListTypesAsync(PublicExamLookupKind.Schedule, CancellationToken.None);
        Assert.Contains(types, type => type.Id == exam.ExamTypeId && type.IdentifierLabel == "Mã sinh viên");
        var scheduleData = await lookup.LookupAsync(
            exam.ExamTypeId, PublicExamLookupKind.Schedule, "SV-ONLINE-01", new DateOnly(2005, 12, 1), CancellationToken.None);
        Assert.Equal("HUMG-IT-01", Assert.Single(scheduleData!.Items).Values["microsoftTeamsRoomName"]);
        var resultData = await lookup.LookupAsync(
            exam.ExamTypeId, PublicExamLookupKind.Results, "SV-ONLINE-01", new DateOnly(2005, 12, 1), CancellationToken.None);
        var result = Assert.Single(resultData!.Items);
        Assert.Equal("7.5", result.Values["multipleChoiceScore"]);
        Assert.NotNull(result.Values["validUntil"]);
    }
}
