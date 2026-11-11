using BaseMVC.Application.Exams;
using BaseMVC.Domain.ExamTypes;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task It_basic_certificate_lookup_uses_identity_number_and_date_of_birth()
    {
        using var factory = new UserManagementWebApplicationFactory();
        int examTypeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var examType = new ExamType
            {
                Name = "Chứng chỉ Ứng dụng Công nghệ Thông tin cơ bản",
                DataFormat = ExamDataFormat.ItBasicCertificate
            };
            var exam = new Exam { Name = "Đợt thi tháng 11", ExamType = examType };
            db.ExamTypes.Add(examType);
            db.Exams.Add(exam);
            db.ItBasicCertificateCandidates.Add(new ItBasicCertificateCandidate
            {
                Exam = exam,
                IdentityNumber = "001205000001",
                CandidateNumber = "CC0001",
                LastName = "Nguyễn Văn",
                FirstName = "An",
                DateOfBirth = new DateOnly(2005, 12, 1),
                ExamDate = new DateOnly(2026, 11, 20),
                ExamRoom = "P.101",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            db.ItBasicCertificateResults.Add(new ItBasicCertificateResult
            {
                Exam = exam,
                IdentityNumber = "001205000001",
                CandidateNumber = "CC0001",
                LastName = "Nguyễn Văn",
                FirstName = "An",
                DateOfBirth = new DateOnly(2005, 12, 1),
                MultipleChoiceScore = "8",
                PracticalScore = "9",
                Result = "Đạt",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            examTypeId = examType.Id;
        }

        await using var lookupScope = factory.Services.CreateAsyncScope();
        var service = lookupScope.ServiceProvider.GetRequiredService<IPublicExamLookupService>();
        var scheduleTypes = await service.ListTypesAsync(PublicExamLookupKind.Schedule, CancellationToken.None);
        Assert.Contains(scheduleTypes, item => item.Id == examTypeId && item.IdentifierLabel == "Căn cước công dân");

        var schedule = await service.LookupAsync(
            examTypeId, PublicExamLookupKind.Schedule, "001205000001", new DateOnly(2005, 12, 1), CancellationToken.None);
        var scheduleItem = Assert.Single(schedule!.Items);
        Assert.Equal("Đợt thi tháng 11", scheduleItem.ExamName);
        Assert.Equal("CC0001", scheduleItem.Values["candidateNumber"]);
        Assert.Equal("20/11/2026", scheduleItem.Values["examDate"]);

        var results = await service.LookupAsync(
            examTypeId, PublicExamLookupKind.Results, "001205000001", new DateOnly(2005, 12, 1), CancellationToken.None);
        var resultItem = Assert.Single(results!.Items);
        Assert.Equal("Đạt", resultItem.Values["result"]);
        Assert.Equal("8", resultItem.Values["multipleChoiceScore"]);

        var wrongDate = await service.LookupAsync(
            examTypeId, PublicExamLookupKind.Results, "001205000001", new DateOnly(2005, 12, 2), CancellationToken.None);
        Assert.Empty(wrongDate!.Items);
    }
}
