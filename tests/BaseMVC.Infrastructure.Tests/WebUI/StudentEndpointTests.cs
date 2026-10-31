using System.Net;
using System.Net.Http.Json;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using ClosedXML.Excel;
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
            StudentPayload(code: "SV00000000000001"),
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

    private static byte[] BuildStudentWorkbook(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Students");
        var headers = new[] { "StudentCode", "LastName", "FirstName", "Gender", "DateOfBirth", "ClassCode", "FacultyCode" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < rows[row].Length; column++)
        {
            var value = rows[row][column];
            var cell = sheet.Cell(row + 2, column + 1);
            if (value is string text) cell.SetValue(text);
            else if (value is bool flag) cell.SetValue(flag);
            else if (value is DateTime date) cell.SetValue(date);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<HttpResponseMessage> PostImportAsync(
        HttpClient client, string token, byte[] content, string fileName = "students.xlsx")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/Students/import") { Content = form };
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Student_import_requires_create_permission()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "student-import-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "StudentImportLimited");
        await factory.SetRolePermissionAsync("StudentImportLimited", "Permissions.Students.Read", grant: true);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");

        using var response = await PostImportAsync(client, token, BuildStudentWorkbook());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousResponse = await PostImportAsync(anonymous, token, BuildStudentWorkbook());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task Student_import_template_requires_permission_and_can_be_imported()
    {
        using var factory = new UserManagementWebApplicationFactory();
        const string email = "student-template-limited@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        await factory.CreateRoleForUserAsync(email, "StudentTemplateLimited");
        await factory.SetRolePermissionAsync("StudentTemplateLimited", "Permissions.Students.Read", grant: true);
        using var limited = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(limited, email, password);
        using var forbidden = await limited.GetAsync("/Admin/Students/import/template");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var unauthorized = await anonymous.GetAsync("/Admin/Students/import/template");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");
        using var faculty = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "CT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, faculty.StatusCode);

        using var template = await client.GetAsync("/Admin/Students/import/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.Equal("StudentImportTemplate.xlsx", template.Content.Headers.ContentDisposition?.FileName);
        var bytes = await template.Content.ReadAsByteArrayAsync();

        using var import = await PostImportAsync(client, token, bytes);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var result = await import.Content.ReadFromJsonAsync<StudentImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Student_import_saves_valid_rows_and_reports_invalid_rows()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");

        using var faculty = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "CT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, faculty.StatusCode);
        using var existing = await PostWithTokenAsync(client, "/Admin/Students", token, StudentPayload(code: "SV0000000009"));
        Assert.Equal(HttpStatusCode.Created, existing.StatusCode);

        using var missingToken = await client.PostAsync("/Admin/Students/import", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using var notExcel = await PostImportAsync(client, token, [1, 2, 3], "students.txt");
        Assert.Equal(HttpStatusCode.BadRequest, notExcel.StatusCode);
        using var corrupted = await PostImportAsync(client, token, [1, 2, 3]);
        Assert.Equal(HttpStatusCode.BadRequest, corrupted.StatusCode);
        using var empty = await PostImportAsync(client, token, BuildStudentWorkbook());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var workbook = BuildStudentWorkbook(
            ["sv0000000001", "Nguyễn", "An", "1", "20/05/2004", "CT01", "ct"],
            ["SV0000000002", "Trần", "Bình", "FALSE", new DateTime(2003, 1, 2), "CT01", "CT"],
            ["SV0000000003", "Lê", "Cường", "true", "", "CT02", "CT"],
            ["SV0000000004", "Phạm", "Dũng", "Nam", "20/05/2004", "CT01", "CT"],
            ["SV0000000005", "Hoàng", "Em", "0", "2004-05-20", "CT01", "CT"],
            ["SV0000000006", "Vũ", "Giang", "0", "31/02/2004", "CT01", "CT"],
            ["SV0000000007", "Đỗ", "Hà", "0", "20/05/2004", "CT01", "ZZ"],
            ["SV0000000009", "Bùi", "Khánh", "0", "20/05/2004", "CT01", "CT"],
            ["SV0000000001", "Trùng", "Trong file", "0", "20/05/2004", "CT01", "CT"],
            ["", "", "", "", "", "", ""],
            ["SV0000000008", "", "Thiếu họ", "0", "20/05/2004", "CT01", "CT"]);
        using var response = await PostImportAsync(client, token, workbook);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StudentImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalRows);
        Assert.Equal(3, result.ImportedCount);
        Assert.Equal([5, 6, 7, 8, 9, 10, 12], result.Errors.Select(error => error.Row).Order());

        var all = await client.GetFromJsonAsync<StudentSearchDto>("/Admin/Students/search?page=1&pageSize=50");
        Assert.NotNull(all);
        Assert.Equal(4, all.TotalCount);

        var first = Assert.Single(all.Items, item => item.StudentCode == "SV0000000001");
        Assert.True(first.Gender);
        Assert.Equal("2004-05-20", first.DateOfBirth);
        Assert.Equal("CT", first.FacultyCode);
        Assert.False(first.EligibleForEnglish);
        Assert.False(first.PassedLanguageRequirement);
        Assert.False(first.PassedITRequirement);
        Assert.Equal("2003-01-02", Assert.Single(all.Items, item => item.StudentCode == "SV0000000002").DateOfBirth);
        Assert.Null(Assert.Single(all.Items, item => item.StudentCode == "SV0000000003").DateOfBirth);
        Assert.Equal("Trần", Assert.Single(all.Items, item => item.StudentCode == "SV0000000002").LastName);
        Assert.Equal("Bình", Assert.Single(all.Items, item => item.StudentCode == "SV0000000002").FirstName);
    }

    [Fact]
    public async Task Student_import_accepts_15_character_codes_and_rejects_more_than_10000_rows()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAdminPageTokenAsync(client, "/Admin/Students");
        using var faculty = await PostWithTokenAsync(
            client, "/Admin/Faculties", token, new { facultyCode = "CT", facultyName = "Công nghệ" });
        Assert.Equal(HttpStatusCode.Created, faculty.StatusCode);

        using var longCode = await PostWithTokenAsync(
            client, "/Admin/Students", token, StudentPayload(code: "ABCDEFGHIJ12345"));
        Assert.Equal(HttpStatusCode.Created, longCode.StatusCode);

        using var hyphenCode = await PostWithTokenAsync(
            client, "/Admin/Students", token, StudentPayload(code: "cttt-10210104"));
        Assert.Equal(HttpStatusCode.Created, hyphenCode.StatusCode);
        var hyphen = await hyphenCode.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal("CTTT-10210104", hyphen?.StudentCode);

        using var badChar = await PostWithTokenAsync(
            client, "/Admin/Students", token, StudentPayload(code: "CTTT_10210104"));
        Assert.Equal(HttpStatusCode.BadRequest, badChar.StatusCode);

        object?[][] Rows(int count) => Enumerable.Range(1, count)
            .Select(index => new object?[] { $"SV{index:D8}", "Họ", "Tên", "0", "01/01/2000", "CT01", "CT" })
            .ToArray();

        using var tooMany = await PostImportAsync(client, token, BuildStudentWorkbook(Rows(10_001)));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);

        using var atLimit = await PostImportAsync(client, token, BuildStudentWorkbook(Rows(10_000)));
        Assert.Equal(HttpStatusCode.OK, atLimit.StatusCode);
        var result = await atLimit.Content.ReadFromJsonAsync<StudentImportResultDto>();
        Assert.NotNull(result);
        Assert.Equal(10_000, result.ImportedCount);
    }

    private sealed record StudentImportRowErrorDto(int Row, string Message);

    private sealed record StudentImportResultDto(
        int TotalRows,
        int ImportedCount,
        IReadOnlyList<StudentImportRowErrorDto> Errors);

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
