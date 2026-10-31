using System.Net;
using System.Net.Http.Json;
using BaseMVC.Domain.Exams;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Exam_endpoints_require_authentication()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Exams/search");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Exam_endpoints_enforce_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "exam-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "ExamLimited");
        await factory.SetRolePermissionAsync("ExamLimited", "Permissions.Exams.Read", grant: true);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        using var page = await client.GetAsync("/Admin/Exams");
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());

        using var read = await client.GetAsync("/Admin/Exams/search");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var options = await client.GetAsync("/Admin/Exams/exam-types");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);

        using var create = await PostWithTokenAsync(client, "/Admin/Exams", token, new { name = "Kỳ thi", examTypeId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var edit = await PostWithTokenAsync(client, "/Admin/Exams/1/edit", token, new { name = "Kỳ thi", examTypeId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        using var delete = await PostWithTokenAsync(client, "/Admin/Exams/1/delete", token);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Exam_crud_validates_and_changes_type_only_before_start()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        var types = await SeedExamTypesAsync(factory, client);
        Assert.NotNull(types);
        Assert.Equal(2, types.Count);
        var typeA = types[0].Id;
        var typeB = types[1].Id;

        using var missingToken = await client.PostAsJsonAsync("/Admin/Exams", new { name = "Kỳ thi 1", examTypeId = typeA });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        foreach (var invalid in new[]
        {
            new { name = "", examTypeId = typeA },
            new { name = new string('x', 501), examTypeId = typeA },
            new { name = "Kỳ thi", examTypeId = 0 },
            new { name = "Kỳ thi", examTypeId = 9999 }
        })
        {
            using var response = await PostWithTokenAsync(client, "/Admin/Exams", token, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var createResponse = await PostWithTokenAsync(
            client, "/Admin/Exams", token, new { name = "  Kỳ thi 1  ", examTypeId = typeA });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExamDto>();
        Assert.NotNull(created);
        Assert.Equal("Kỳ thi 1", created.Name);
        Assert.Equal("NotStarted", created.Status);
        Assert.False(created.IsDeleted);

        // Names are not unique.
        using var sameName = await PostWithTokenAsync(
            client, "/Admin/Exams", token, new { name = "Kỳ thi 1", examTypeId = typeA });
        Assert.Equal(HttpStatusCode.Created, sameName.StatusCode);

        using var editResponse = await PostWithTokenAsync(
            client, $"/Admin/Exams/{created.Id}/edit", token, new { name = "Kỳ thi 1 mới", examTypeId = typeB });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var edited = await editResponse.Content.ReadFromJsonAsync<ExamDto>();
        Assert.NotNull(edited);
        Assert.Equal(typeB, edited.ExamTypeId);
        Assert.NotNull(edited.LastModifiedAtUtc);

        await SetExamStatusAsync(factory, created.Id, ExamStatus.RegistrationOpen);

        using var renameOnly = await PostWithTokenAsync(
            client, $"/Admin/Exams/{created.Id}/edit", token, new { name = "Đổi tên", examTypeId = typeB });
        Assert.Equal(HttpStatusCode.OK, renameOnly.StatusCode);

        using var changeType = await PostWithTokenAsync(
            client, $"/Admin/Exams/{created.Id}/edit", token, new { name = "Đổi tên", examTypeId = typeA });
        Assert.Equal(HttpStatusCode.Conflict, changeType.StatusCode);

        using var editMissing = await PostWithTokenAsync(
            client, "/Admin/Exams/9999/edit", token, new { name = "Không có", examTypeId = typeA });
        Assert.Equal(HttpStatusCode.NotFound, editMissing.StatusCode);
    }

    [Theory]
    [InlineData("/js/admin-exams.js")]
    [InlineData("/js/admin-exam-types.js")]
    public async Task Admin_scripts_declare_adminApi_before_using_it(string path)
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient();

        var script = await client.GetStringAsync(path);

        Assert.Contains("const adminApi = window.BaseAdmin;", script);
    }

    [Fact]
    public async Task Exam_search_filters_by_name_type_status_and_deleted()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        var types = await SeedExamTypesAsync(factory, client);
        Assert.NotNull(types);
        var typeA = types[0].Id;
        var typeB = types[1].Id;

        var first = await CreateExamAsync(client, token, "Đợt Tháng Một", typeA);
        await CreateExamAsync(client, token, "Đợt tháng hai", typeB);
        await CreateExamAsync(client, token, "Khác", typeB);
        await SetExamStatusAsync(factory, first.Id, ExamStatus.Taken);

        var byName = await client.GetFromJsonAsync<ExamSearchDto>($"/Admin/Exams/search?search={Uri.EscapeDataString("Đợt Tháng".ToUpperInvariant())}&page=1&pageSize=10");
        Assert.NotNull(byName);
        Assert.Equal(2, byName.TotalCount);

        var byType = await client.GetFromJsonAsync<ExamSearchDto>($"/Admin/Exams/search?examTypeId={typeB}&page=1&pageSize=10");
        Assert.NotNull(byType);
        Assert.Equal(2, byType.TotalCount);

        var byStatus = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search?status=Taken&page=1&pageSize=10");
        Assert.NotNull(byStatus);
        Assert.Equal(first.Id, Assert.Single(byStatus.Items).Id);

        var paged = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search?page=1&pageSize=1");
        Assert.NotNull(paged);
        Assert.Equal(3, paged.TotalCount);
        Assert.Single(paged.Items);

        using var delete = await PostWithTokenAsync(client, $"/Admin/Exams/{first.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var hidden = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search?page=1&pageSize=10");
        Assert.NotNull(hidden);
        Assert.Equal(2, hidden.TotalCount);

        var shown = await client.GetFromJsonAsync<ExamSearchDto>("/Admin/Exams/search?includeDeleted=true&page=1&pageSize=10");
        Assert.NotNull(shown);
        Assert.Equal(3, shown.TotalCount);
        Assert.True(Assert.Single(shown.Items, item => item.Id == first.Id).IsDeleted);
    }

    [Fact]
    public async Task Exam_delete_is_hard_when_not_started_and_soft_otherwise()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        var types = await SeedExamTypesAsync(factory, client);
        Assert.NotNull(types);
        var notStarted = await CreateExamAsync(client, token, "Chưa bắt đầu", types[0].Id);
        var started = await CreateExamAsync(client, token, "Đã đăng ký", types[0].Id);
        await SetExamStatusAsync(factory, started.Id, ExamStatus.RegistrationOpen);

        using var hardDelete = await PostWithTokenAsync(client, $"/Admin/Exams/{notStarted.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, hardDelete.StatusCode);
        using var softDelete = await PostWithTokenAsync(client, $"/Admin/Exams/{started.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, softDelete.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await dbContext.Exams.IgnoreQueryFilters().AnyAsync(exam => exam.Id == notStarted.Id));
            var row = await dbContext.Exams.IgnoreQueryFilters().SingleAsync(exam => exam.Id == started.Id);
            Assert.True(row.IsDeleted);
            Assert.Equal(ExamStatus.RegistrationOpen, row.Status);
        }

        using var again = await PostWithTokenAsync(client, $"/Admin/Exams/{started.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        using var editDeleted = await PostWithTokenAsync(
            client, $"/Admin/Exams/{started.Id}/edit", token, new { name = "Sửa", examTypeId = types[0].Id });
        Assert.Equal(HttpStatusCode.NotFound, editDeleted.StatusCode);
    }

    [Fact]
    public async Task ExamType_delete_is_blocked_while_exams_reference_it()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        var types = await SeedExamTypesAsync(factory, client);
        Assert.NotNull(types);
        var used = types[0].Id;
        var unused = types[1].Id;
        var exam = await CreateExamAsync(client, token, "Kỳ thi", used);

        using var blocked = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{used}/delete", token);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // A soft-deleted exam still blocks removal of its type.
        await SetExamStatusAsync(factory, exam.Id, ExamStatus.Completed);
        using var softDelete = await PostWithTokenAsync(client, $"/Admin/Exams/{exam.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, softDelete.StatusCode);
        using var stillBlocked = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{used}/delete", token);
        Assert.Equal(HttpStatusCode.Conflict, stillBlocked.StatusCode);

        using var allowed = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{unused}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public void Administrator_role_receives_exam_permissions()
    {
        var permissions = PermissionCatalog.ForRole(IdentitySeedRoles.Administrator)
            .Select(permission => permission.Name)
            .ToList();

        Assert.Contains("Permissions.Exams.Read", permissions);
        Assert.Contains("Permissions.Exams.Create", permissions);
        Assert.Contains("Permissions.Exams.Update", permissions);
        Assert.Contains("Permissions.Exams.Delete", permissions);
        Assert.Contains("Permissions.Exams.ChangeStatus", permissions);
    }

    private static async Task<ExamDto> CreateExamAsync(HttpClient client, string token, string name, int examTypeId)
    {
        using var response = await PostWithTokenAsync(client, "/Admin/Exams", token, new { name, examTypeId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var exam = await response.Content.ReadFromJsonAsync<ExamDto>();
        Assert.NotNull(exam);
        return exam;
    }

    private static async Task SetExamStatusAsync(UserManagementWebApplicationFactory factory, int id, ExamStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exam = await dbContext.Exams.IgnoreQueryFilters().SingleAsync(item => item.Id == id);
        exam.Status = status;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<List<ExamTypeOptionDto>> SeedExamTypesAsync(
        UserManagementWebApplicationFactory factory, HttpClient client)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.ExamTypes.AddRange(
                new BaseMVC.Domain.ExamTypes.ExamType { Name = "Loại A" },
                new BaseMVC.Domain.ExamTypes.ExamType { Name = "Loại B" });
            await dbContext.SaveChangesAsync();
        }

        var types = await client.GetFromJsonAsync<List<ExamTypeOptionDto>>("/Admin/Exams/exam-types");
        Assert.NotNull(types);
        return types;
    }

    private sealed record ExamTypeOptionDto(int Id, string Name);

    private sealed record ExamDto(
        int Id,
        string Name,
        int ExamTypeId,
        string ExamTypeName,
        string Status,
        bool IsDeleted,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastModifiedAtUtc);

    private sealed record ExamSearchDto(
        IReadOnlyList<ExamDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
