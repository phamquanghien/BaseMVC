using System.Net;
using System.Net.Http.Json;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Faculty_endpoints_require_authentication()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Faculties/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Faculty_endpoints_enforce_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "faculty-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "FacultyLimited");
        await factory.SetRolePermissionAsync("FacultyLimited", "Permissions.Faculties.Read", grant: true);

        using var superClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(superClient, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var superToken = await GetAntiforgeryTokenAsync(superClient);
        using var seeded = await PostWithTokenAsync(
            superClient, "/Admin/Faculties", superToken, new { facultyCode = "CT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, seeded.StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        using var page = await client.GetAsync("/Admin/Faculties");
        page.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());

        using var read = await client.GetAsync("/Admin/Faculties/search");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var create = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "KT", facultyName = "Kinh tế" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var edit = await PostWithTokenAsync(
            client, "/Admin/Faculties/1/edit", token, new { facultyCode = "KT", facultyName = "Kinh tế" });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);

        using var delete = await PostWithTokenAsync(client, "/Admin/Faculties/1/delete", token);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Faculty_crud_validates_normalizes_and_enforces_unique_code()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        using var missingToken = await client.PostAsJsonAsync(
            "/Admin/Faculties", new { facultyCode = "CT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        foreach (var invalid in new[]
        {
            new { facultyCode = "C", facultyName = "Ngắn" },
            new { facultyCode = "CTT", facultyName = "Dài" },
            new { facultyCode = "C#", facultyName = "Ký tự đặc biệt" },
            new { facultyCode = "CT", facultyName = "" },
            new { facultyCode = "CT", facultyName = new string('x', 201) }
        })
        {
            using var response = await PostWithTokenAsync(client, "/Admin/Faculties", token, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var createResponse = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "ct", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<FacultyDto>();
        Assert.NotNull(created);
        Assert.Equal("CT", created.FacultyCode);
        Assert.Null(created.LastModifiedAtUtc);

        using var duplicate = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "CT", facultyName = "Trùng" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var other = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "KT", facultyName = "Kinh tế" });
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        using var editConflict = await PostWithTokenAsync(
            client, $"/Admin/Faculties/{created.Id}/edit", token, new { facultyCode = "KT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Conflict, editConflict.StatusCode);

        using var editResponse = await PostWithTokenAsync(
            client, $"/Admin/Faculties/{created.Id}/edit", token, new { facultyCode = "cn", facultyName = "Công nghệ mới" });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var edited = await editResponse.Content.ReadFromJsonAsync<FacultyDto>();
        Assert.NotNull(edited);
        Assert.Equal("CN", edited.FacultyCode);
        Assert.Equal("Công nghệ mới", edited.FacultyName);
        Assert.NotNull(edited.LastModifiedAtUtc);

        using var editMissing = await PostWithTokenAsync(
            client, "/Admin/Faculties/9999/edit", token, new { facultyCode = "ZZ", facultyName = "Không có" });
        Assert.Equal(HttpStatusCode.NotFound, editMissing.StatusCode);

        var search = await client.GetFromJsonAsync<FacultySearchDto>("/Admin/Faculties/search?search=Kinh&page=1&pageSize=10");
        Assert.NotNull(search);
        Assert.Equal("KT", Assert.Single(search.Items).FacultyCode);

        var paged = await client.GetFromJsonAsync<FacultySearchDto>("/Admin/Faculties/search?page=1&pageSize=1");
        Assert.NotNull(paged);
        Assert.Equal(2, paged.TotalCount);
        Assert.Single(paged.Items);

        using var deleteResponse = await PostWithTokenAsync(client, $"/Admin/Faculties/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var deleteMissing = await PostWithTokenAsync(client, $"/Admin/Faculties/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NotFound, deleteMissing.StatusCode);
    }

    [Fact]
    public void Administrator_role_receives_faculty_permissions()
    {
        var permissions = PermissionCatalog.ForRole(IdentitySeedRoles.Administrator)
            .Select(permission => permission.Name)
            .ToList();

        Assert.Contains("Permissions.Faculties.Read", permissions);
        Assert.Contains("Permissions.Faculties.Create", permissions);
        Assert.Contains("Permissions.Faculties.Update", permissions);
        Assert.Contains("Permissions.Faculties.Delete", permissions);
    }

    private sealed record FacultyDto(
        int Id,
        string FacultyCode,
        string FacultyName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastModifiedAtUtc);

    private sealed record FacultySearchDto(
        IReadOnlyList<FacultyDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
