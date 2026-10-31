using System.Net;
using System.Net.Http.Json;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task ExamType_endpoints_require_authentication()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/ExamTypes/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExamType_endpoints_enforce_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "exam-type-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "ExamTypeLimited");
        await factory.SetRolePermissionAsync("ExamTypeLimited", "Permissions.ExamTypes.Read", grant: true);

        using var superClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(superClient, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var superToken = await GetAntiforgeryTokenAsync(superClient);
        using var seeded = await PostWithTokenAsync(
            superClient, "/Admin/ExamTypes", superToken, new { name = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, seeded.StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        using var page = await client.GetAsync("/Admin/ExamTypes");
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());

        using var read = await client.GetAsync("/Admin/ExamTypes/search");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var create = await PostWithTokenAsync(
            client, "/Admin/ExamTypes", token, new { name = "Kinh tế" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var edit = await PostWithTokenAsync(
            client, "/Admin/ExamTypes/1/edit", token, new { name = "Kinh tế" });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);

        using var delete = await PostWithTokenAsync(client, "/Admin/ExamTypes/1/delete", token);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task ExamType_crud_validates_normalizes_and_enforces_unique_name()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        using var missingToken = await client.PostAsJsonAsync("/Admin/ExamTypes", new { name = "Kỳ thi A" });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        foreach (var invalid in new[] { new { name = "" }, new { name = "   " }, new { name = new string('x', 501) } })
        {
            using var response = await PostWithTokenAsync(client, "/Admin/ExamTypes", token, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var createResponse = await PostWithTokenAsync(
            client, "/Admin/ExamTypes", token, new { name = "  Kỳ thi A  " });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExamTypeDto>();
        Assert.NotNull(created);
        Assert.Equal("Kỳ thi A", created.Name);
        Assert.Null(created.LastModifiedAtUtc);

        using var duplicate = await PostWithTokenAsync(client, "/Admin/ExamTypes", token, new { name = "KỲ THI a" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var other = await PostWithTokenAsync(client, "/Admin/ExamTypes", token, new { name = "Kỳ thi B" });
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        using var editConflict = await PostWithTokenAsync(
            client, $"/Admin/ExamTypes/{created.Id}/edit", token, new { name = "Kỳ thi B" });
        Assert.Equal(HttpStatusCode.Conflict, editConflict.StatusCode);

        using var editResponse = await PostWithTokenAsync(
            client, $"/Admin/ExamTypes/{created.Id}/edit", token, new { name = "Kỳ thi A mới" });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var edited = await editResponse.Content.ReadFromJsonAsync<ExamTypeDto>();
        Assert.NotNull(edited);
        Assert.Equal("Kỳ thi A mới", edited.Name);
        Assert.NotNull(edited.LastModifiedAtUtc);

        using var editMissing = await PostWithTokenAsync(
            client, "/Admin/ExamTypes/9999/edit", token, new { name = "Không có" });
        Assert.Equal(HttpStatusCode.NotFound, editMissing.StatusCode);

        var search = await client.GetFromJsonAsync<ExamTypeSearchDto>("/Admin/ExamTypes/search?search=thi%20b&page=1&pageSize=10");
        Assert.NotNull(search);
        Assert.Equal("Kỳ thi B", Assert.Single(search.Items).Name);

        var unicode = await client.GetFromJsonAsync<ExamTypeSearchDto>("/Admin/ExamTypes/search?search=K%E1%BB%B2%20THI%20B&page=1&pageSize=10");
        Assert.NotNull(unicode);
        Assert.Equal("Kỳ thi B", Assert.Single(unicode.Items).Name);

        var paged = await client.GetFromJsonAsync<ExamTypeSearchDto>("/Admin/ExamTypes/search?page=1&pageSize=1");
        Assert.NotNull(paged);
        Assert.Equal(2, paged.TotalCount);
        Assert.Single(paged.Items);

        using var deleteResponse = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var deleteMissing = await PostWithTokenAsync(client, $"/Admin/ExamTypes/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NotFound, deleteMissing.StatusCode);
    }

    [Fact]
    public void Administrator_role_receives_examType_permissions()
    {
        var permissions = PermissionCatalog.ForRole(IdentitySeedRoles.Administrator)
            .Select(permission => permission.Name)
            .ToList();

        Assert.Contains("Permissions.ExamTypes.Read", permissions);
        Assert.Contains("Permissions.ExamTypes.Create", permissions);
        Assert.Contains("Permissions.ExamTypes.Update", permissions);
        Assert.Contains("Permissions.ExamTypes.Delete", permissions);
    }

    private sealed record ExamTypeDto(
        int Id,
        string Name,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastModifiedAtUtc);

    private sealed record ExamTypeSearchDto(
        IReadOnlyList<ExamTypeDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
