using System.Net;
using System.Net.Http.Json;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    private static object StudentPayload(
        string code = "SV0000000001",
        string facultyCode = "CT",
        string lastName = "Nguyễn",
        string firstName = "An",
        string classCode = "CT01",
        bool gender = false,
        bool english = false,
        string? dateOfBirth = null) => new
        {
            studentCode = code,
            lastName,
            firstName,
            classCode,
            facultyCode,
            gender,
            eligibleForEnglish = english,
            passedLanguageRequirement = false,
            passedITRequirement = false,
            dateOfBirth
        };

    private static async Task<string> GetAdminPageTokenAsync(HttpClient client, string path)
    {
        using var page = await client.GetAsync(path);
        page.EnsureSuccessStatusCode();
        return ExtractAntiforgeryToken(await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Student_endpoints_require_authentication()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Admin/Students/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Student_endpoints_enforce_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "student-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "StudentLimited");
        await factory.SetRolePermissionAsync("StudentLimited", "Permissions.Students.Read", grant: true);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");

        using var read = await client.GetAsync("/Admin/Students/search");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var options = await client.GetAsync("/Admin/Students/faculty-options");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);

        using var create = await PostWithTokenAsync(client, "/Admin/Students", token, StudentPayload());
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var edit = await PostWithTokenAsync(client, "/Admin/Students/1/edit", token, StudentPayload());
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        using var delete = await PostWithTokenAsync(client, "/Admin/Students/1/delete", token);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Student_crud_validates_links_faculty_and_enforces_unique_code()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");

        foreach (var (code, name) in new[] { ("CT", "Công nghệ"), ("KT", "Kinh tế") })
        {
            using var faculty = await PostWithTokenAsync(
                client, "/Admin/Faculties", token, new { facultyCode = code, facultyName = name });
            Assert.Equal(HttpStatusCode.Created, faculty.StatusCode);
        }

        var facultyOptions = await client.GetFromJsonAsync<List<FacultyOptionDto>>("/Admin/Students/faculty-options");
        Assert.NotNull(facultyOptions);
        Assert.Equal(["CT", "KT"], facultyOptions.Select(option => option.FacultyCode));

        using var missingToken = await client.PostAsJsonAsync("/Admin/Students", StudentPayload());
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        foreach (var invalid in new[]
        {
            StudentPayload(code: ""),
            StudentPayload(code: "SV 000001"),
            StudentPayload(code: "SV00000000001"),
            StudentPayload(lastName: ""),
            StudentPayload(firstName: ""),
            StudentPayload(classCode: new string('c', 201)),
            StudentPayload(facultyCode: "C"),
            StudentPayload(facultyCode: "ZZ"),
            StudentPayload(dateOfBirth: "2999-01-01")
        })
        {
            using var response = await PostWithTokenAsync(client, "/Admin/Students", token, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var createResponse = await PostWithTokenAsync(
            client, "/Admin/Students", token,
            StudentPayload(code: "sv0000000001", gender: true, english: true, dateOfBirth: "2004-05-20"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<StudentDto>();
        Assert.NotNull(created);
        Assert.Equal("SV0000000001", created.StudentCode);
        Assert.Equal("Công nghệ", created.FacultyName);
        Assert.True(created.Gender);
        Assert.True(created.EligibleForEnglish);
        Assert.False(created.PassedITRequirement);
        Assert.Equal("2004-05-20", created.DateOfBirth);
        Assert.Null(created.LastModifiedAtUtc);

        using var duplicate = await PostWithTokenAsync(client, "/Admin/Students", token, StudentPayload());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var other = await PostWithTokenAsync(
            client, "/Admin/Students", token,
            StudentPayload(code: "SV0000000002", facultyCode: "KT", lastName: "Trần", firstName: "Bình", classCode: "KT01"));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        var otherStudent = await other.Content.ReadFromJsonAsync<StudentDto>();
        Assert.NotNull(otherStudent);
        Assert.False(otherStudent.Gender);
        Assert.Null(otherStudent.DateOfBirth);

        using var editConflict = await PostWithTokenAsync(
            client, $"/Admin/Students/{created.Id}/edit", token, StudentPayload(code: "SV0000000002"));
        Assert.Equal(HttpStatusCode.Conflict, editConflict.StatusCode);

        using var editResponse = await PostWithTokenAsync(
            client, $"/Admin/Students/{created.Id}/edit", token,
            StudentPayload(facultyCode: "kt", firstName: "Anh", classCode: "KT02"));
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var edited = await editResponse.Content.ReadFromJsonAsync<StudentDto>();
        Assert.NotNull(edited);
        Assert.Equal("KT", edited.FacultyCode);
        Assert.Equal("Anh", edited.FirstName);
        Assert.False(edited.Gender);
        Assert.NotNull(edited.LastModifiedAtUtc);

        using var editMissing = await PostWithTokenAsync(client, "/Admin/Students/9999/edit", token, StudentPayload(code: "SV0000000003"));
        Assert.Equal(HttpStatusCode.NotFound, editMissing.StatusCode);

        var byName = await client.GetFromJsonAsync<StudentSearchDto>("/Admin/Students/search?search=tr%E1%BA%A7n&page=1&pageSize=10");
        Assert.NotNull(byName);
        Assert.Equal("SV0000000002", Assert.Single(byName.Items).StudentCode);

        var byCode = await client.GetFromJsonAsync<StudentSearchDto>("/Admin/Students/search?search=sv0000000001");
        Assert.NotNull(byCode);
        Assert.Equal(created.Id, Assert.Single(byCode.Items).Id);

        var paged = await client.GetFromJsonAsync<StudentSearchDto>("/Admin/Students/search?page=1&pageSize=1");
        Assert.NotNull(paged);
        Assert.Equal(2, paged.TotalCount);
        Assert.Single(paged.Items);

        using var deleteResponse = await PostWithTokenAsync(client, $"/Admin/Students/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var deleteMissing = await PostWithTokenAsync(client, $"/Admin/Students/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NotFound, deleteMissing.StatusCode);
    }

    [Fact]
    public async Task Faculty_with_students_cannot_be_deleted_or_recoded()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");

        using var facultyResponse = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "CT", facultyName = "Công nghệ" });
        var faculty = await facultyResponse.Content.ReadFromJsonAsync<FacultyDto>();
        Assert.NotNull(faculty);
        using var student = await PostWithTokenAsync(client, "/Admin/Students", token, StudentPayload());
        Assert.Equal(HttpStatusCode.Created, student.StatusCode);
        var created = await student.Content.ReadFromJsonAsync<StudentDto>();
        Assert.NotNull(created);

        using var recode = await PostWithTokenAsync(
            client, $"/Admin/Faculties/{faculty.Id}/edit", token, new { facultyCode = "CN", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Conflict, recode.StatusCode);

        using var rename = await PostWithTokenAsync(
            client, $"/Admin/Faculties/{faculty.Id}/edit", token, new { facultyCode = "CT", facultyName = "CNTT" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        using var delete = await PostWithTokenAsync(client, $"/Admin/Faculties/{faculty.Id}/delete", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        using var deleteStudent = await PostWithTokenAsync(client, $"/Admin/Students/{created.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteStudent.StatusCode);
        using var deleteFaculty = await PostWithTokenAsync(client, $"/Admin/Faculties/{faculty.Id}/delete", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteFaculty.StatusCode);
    }

    [Fact]
    public void Administrator_role_receives_student_permissions()
    {
        var permissions = PermissionCatalog.ForRole(IdentitySeedRoles.Administrator)
            .Select(permission => permission.Name)
            .ToList();

        foreach (var action in new[] { "Read", "Create", "Update", "Delete" })
            Assert.Contains($"Permissions.Students.{action}", permissions);
    }

    private sealed record FacultyOptionDto(string FacultyCode, string FacultyName);

    private sealed record StudentDto(
        int Id,
        string StudentCode,
        string LastName,
        string FirstName,
        string ClassCode,
        string FacultyCode,
        string? FacultyName,
        bool Gender,
        bool EligibleForEnglish,
        bool PassedLanguageRequirement,
        bool PassedITRequirement,
        string? DateOfBirth,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastModifiedAtUtc);

    private sealed record StudentSearchDto(
        IReadOnlyList<StudentDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
