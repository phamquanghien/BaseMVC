using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed class UserManagementEndpointTests
{
    [Fact]
    public async Task User_crud_requires_antiforgery_and_protects_current_and_last_super_admin()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);

        var token = await GetAntiforgeryTokenAsync(client);
        var roles = await client.GetFromJsonAsync<List<RoleDto>>("/Admin/Users/roles");
        Assert.NotNull(roles);
        var administratorRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.Administrator);

        using var missingTokenResponse = await client.PostAsJsonAsync(
            "/Admin/Users",
            new
            {
                email = "without-token@example.test",
                password = "InitialUser123!",
                roleIds = Array.Empty<string>()
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using var createResponse = await PostWithTokenAsync(
            client,
            "/Admin/Users",
            token,
            new
            {
                email = "created-user@example.test",
                password = "InitialUser123!",
                roleIds = Array.Empty<string>()
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);

        var search = await client.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=created-user%40example.test&page=1&pageSize=10");
        Assert.NotNull(search);
        Assert.Equal(1, search.TotalCount);
        Assert.Equal(created.Id, Assert.Single(search.Items).Id);

        using var editResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{created.Id}/edit",
            token,
            new
            {
                email = "updated-user@example.test",
                roleIds = new[] { administratorRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var updated = await editResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(updated);
        Assert.Equal("updated-user@example.test", updated.Email);
        Assert.True(updated.IsLocked);
        Assert.Contains(IdentitySeedRoles.Administrator, updated.RoleNames);

        using var unlockResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{created.Id}/edit",
            token,
            new
            {
                email = "updated-user@example.test",
                roleIds = Array.Empty<string>(),
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);
        var unlocked = await unlockResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(unlocked);
        Assert.False(unlocked.IsLocked);
        Assert.Empty(unlocked.RoleNames);

        using var deleteResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{created.Id}/delete",
            token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var superAdminSearch = await client.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=integration-superadmin%40example.test");
        Assert.NotNull(superAdminSearch);
        var superAdmin = Assert.Single(superAdminSearch.Items);

        using var selfLockResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{superAdmin.Id}/edit",
            token,
            new
            {
                email = factory.SuperAdminEmail,
                roleIds = new[] { administratorRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.Conflict, selfLockResponse.StatusCode);

        using var selfDeleteResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{superAdmin.Id}/delete",
            token);
        Assert.Equal(HttpStatusCode.Conflict, selfDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Administrator_cannot_assign_or_remove_system_roles()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var superAdminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(superAdminClient, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var superAdminToken = await GetAntiforgeryTokenAsync(superAdminClient);
        var roles = await superAdminClient.GetFromJsonAsync<List<RoleDto>>("/Admin/Users/roles");
        Assert.NotNull(roles);
        var administratorRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.Administrator);
        var superAdminRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.SuperAdmin);

        using var createAdminResponse = await PostWithTokenAsync(
            superAdminClient,
            "/Admin/Users",
            superAdminToken,
            new
            {
                email = "integration-administrator@example.test",
                password = "Administrator123!",
                roleIds = new[] { administratorRole.Id }
            });
        Assert.Equal(HttpStatusCode.Created, createAdminResponse.StatusCode);

        using var administratorClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(
            administratorClient,
            "integration-administrator@example.test",
            "Administrator123!");
        var administratorToken = await GetAntiforgeryTokenAsync(administratorClient);

        var availableRoles = await administratorClient.GetFromJsonAsync<List<RoleDto>>("/Admin/Users/roles");
        Assert.NotNull(availableRoles);
        Assert.All(
            availableRoles.Where(role => role.Name is IdentitySeedRoles.Administrator or IdentitySeedRoles.SuperAdmin),
            role => Assert.False(role.IsAssignable));

        using var assignSystemRoleResponse = await PostWithTokenAsync(
            administratorClient,
            "/Admin/Users",
            administratorToken,
            new
            {
                email = "escalation-attempt@example.test",
                password = "InitialUser123!",
                roleIds = new[] { superAdminRole.Id }
            });
        Assert.Equal(HttpStatusCode.Forbidden, assignSystemRoleResponse.StatusCode);

        var self = await administratorClient.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=integration-administrator%40example.test");
        Assert.NotNull(self);
        var administrator = Assert.Single(self.Items);

        using var selfLockResponse = await PostWithTokenAsync(
            administratorClient,
            $"/Admin/Users/{administrator.Id}/edit",
            administratorToken,
            new
            {
                email = "integration-administrator@example.test",
                roleIds = new[] { administratorRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.Conflict, selfLockResponse.StatusCode);

        using var selfDeleteResponse = await PostWithTokenAsync(
            administratorClient,
            $"/Admin/Users/{administrator.Id}/delete",
            administratorToken);
        Assert.Equal(HttpStatusCode.Conflict, selfDeleteResponse.StatusCode);

        using var removeSystemRoleResponse = await PostWithTokenAsync(
            administratorClient,
            $"/Admin/Users/{administrator.Id}/edit",
            administratorToken,
            new
            {
                email = "integration-administrator@example.test",
                roleIds = Array.Empty<string>(),
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.Forbidden, removeSystemRoleResponse.StatusCode);

        using var createPrivilegedResponse = await PostWithTokenAsync(
            superAdminClient,
            "/Admin/Users",
            superAdminToken,
            new
            {
                email = "integration-privileged-admin@example.test",
                password = "PrivilegedAdmin123!",
                roleIds = new[] { administratorRole.Id }
            });
        Assert.Equal(HttpStatusCode.Created, createPrivilegedResponse.StatusCode);
        await factory.GrantAccessManagementAsync("integration-privileged-admin@example.test");

        using var privilegedClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(
            privilegedClient,
            "integration-privileged-admin@example.test",
            "PrivilegedAdmin123!");
        var privilegedToken = await GetAntiforgeryTokenAsync(privilegedClient);
        var superAdminSearch = await privilegedClient.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=integration-superadmin%40example.test");
        Assert.NotNull(superAdminSearch);
        var lastSuperAdmin = Assert.Single(superAdminSearch.Items);

        using var lockLastSuperAdminResponse = await PostWithTokenAsync(
            privilegedClient,
            $"/Admin/Users/{lastSuperAdmin.Id}/edit",
            privilegedToken,
            new
            {
                email = factory.SuperAdminEmail,
                roleIds = new[] { superAdminRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.Conflict, lockLastSuperAdminResponse.StatusCode);

        using var deleteLastSuperAdminResponse = await PostWithTokenAsync(
            privilegedClient,
            $"/Admin/Users/{lastSuperAdmin.Id}/delete",
            privilegedToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteLastSuperAdminResponse.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_change_password_with_current_password_and_antiforgery_token()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);

        using var pageResponse = await client.GetAsync("/Account/ChangePassword");
        pageResponse.EnsureSuccessStatusCode();
        var page = await pageResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(page);

        using var missingTokenResponse = await client.PostAsync(
            "/Account/ChangePassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["CurrentPassword"] = factory.SuperAdminPassword,
                ["NewPassword"] = "UpdatedPassword123!",
                ["ConfirmPassword"] = "UpdatedPassword123!"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using var invalidCurrentPasswordResponse = await client.PostAsync(
            "/Account/ChangePassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CurrentPassword"] = "IncorrectPassword123!",
                ["NewPassword"] = "UpdatedPassword123!",
                ["ConfirmPassword"] = "UpdatedPassword123!"
            }));
        Assert.Equal(HttpStatusCode.OK, invalidCurrentPasswordResponse.StatusCode);
        Assert.Contains(
            "Mật khẩu hiện tại không chính xác.",
            WebUtility.HtmlDecode(await invalidCurrentPasswordResponse.Content.ReadAsStringAsync()),
            StringComparison.Ordinal);

        using var changeResponse = await client.PostAsync(
            "/Account/ChangePassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CurrentPassword"] = factory.SuperAdminPassword,
                ["NewPassword"] = "UpdatedPassword123!",
                ["ConfirmPassword"] = "UpdatedPassword123!"
            }));
        Assert.Equal(HttpStatusCode.Redirect, changeResponse.StatusCode);
        Assert.EndsWith("/Account/ChangePassword", changeResponse.Headers.Location?.OriginalString);

        await SignOutAsync(client);
        await AssertLoginResponseAsync(
            client,
            factory.SuperAdminEmail,
            factory.SuperAdminPassword,
            HttpStatusCode.OK);
        await AssertLoginResponseAsync(
            client,
            factory.SuperAdminEmail,
            "UpdatedPassword123!",
            HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Health_endpoints_are_anonymous_and_report_liveness_and_database_readiness()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var livenessResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal("Healthy", await livenessResponse.Content.ReadAsStringAsync());

        using var readinessResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        Assert.Equal("Healthy", await readinessResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_fails_when_sqlite_is_unavailable_while_liveness_remains_healthy()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var initialReadinessResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, initialReadinessResponse.StatusCode);

        factory.PointToUnavailableDatabase();

        using var livenessResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal("Healthy", await livenessResponse.Content.ReadAsStringAsync());

        using var readinessResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal("Unhealthy", await readinessResponse.Content.ReadAsStringAsync());
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        using var loginPageResponse = await client.GetAsync("/Account/Login");
        loginPageResponse.EnsureSuccessStatusCode();
        var loginPage = await loginPageResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(loginPage);
        using var loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
    }

    private static async Task AssertLoginResponseAsync(
        HttpClient client,
        string email,
        string password,
        HttpStatusCode expectedStatus)
    {
        using var loginPageResponse = await client.GetAsync("/Account/Login");
        loginPageResponse.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await loginPageResponse.Content.ReadAsStringAsync());
        using var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static async Task SignOutAsync(HttpClient client)
    {
        using var pageResponse = await client.GetAsync("/Account/ChangePassword");
        pageResponse.EnsureSuccessStatusCode();
        var token = ExtractAntiforgeryToken(await pageResponse.Content.ReadAsStringAsync());
        using var response = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnUrl"] = "/"
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Admin/Users");
        response.EnsureSuccessStatusCode();
        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The page did not contain an Anti-Forgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string path,
        string token,
        object? payload = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("RequestVerificationToken", token);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request);
    }

    private sealed record RoleDto(string Id, string Name, bool IsAssignable);

    private sealed record UserDto(
        string Id,
        string Email,
        bool IsLocked,
        bool IsCurrentUser,
        IReadOnlyList<string> RoleNames);

    private sealed record UserSearchDto(
        IReadOnlyList<UserDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed class UserManagementWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public string SuperAdminEmail { get; } = "integration-superadmin@example.test";

        public string SuperAdminPassword { get; } = "IntegrationAdmin123!";

        public UserManagementWebApplicationFactory()
        {
            _connection.Open();
        }

        public async Task GrantAccessManagementAsync(string email)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var result = await userManager.AddClaimAsync(
                user,
                new Claim(PermissionClaim.Type, "Permissions.Access.Manage"));
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        public void PointToUnavailableDatabase()
        {
            _connection.Close();
            _connection.ConnectionString =
                "Data Source=/basemvc-health-check-unavailable/health.db;Mode=ReadOnly";
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                    ["SeedAdmin:Email"] = SuperAdminEmail,
                    ["SeedAdmin:Password"] = SuperAdminPassword
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}
