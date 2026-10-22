using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.WebUI.Email;
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
using Microsoft.AspNetCore.WebUtilities;

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

        var auditEntries = await GetAuditEntriesAsync(factory);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.UserCreated)
            && entry.Outcome == nameof(AuditOutcome.Succeeded)
            && entry.TargetUserId == created.Id);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.UserUpdated)
            && entry.Outcome == nameof(AuditOutcome.Succeeded)
            && entry.TargetUserId == created.Id);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.UserDeleted)
            && entry.Outcome == nameof(AuditOutcome.Succeeded)
            && entry.TargetUserId == created.Id);
    }

    [Fact]
    public async Task Role_and_permission_mutations_are_audited_with_stable_role_id()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);
        var token = await GetAntiforgeryTokenAsync(client);

        using var createResponse = await PostWithTokenAsync(
            client,
            "/Admin/Roles",
            token,
            new { name = "AuditTestRole" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdRole = await createResponse.Content.ReadFromJsonAsync<RoleMutationResponse>();
        Assert.NotNull(createdRole);

        using var editResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Roles/{createdRole.Id}/edit",
            token,
            new { name = "AuditUpdatedRole" });
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        using var permissionsResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Permissions/roles/{createdRole.Id}",
            token,
            new { permissions = new[] { "Permissions.Users.Read" } });
        Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);

        using var deleteResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Roles/{createdRole.Id}/delete",
            token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var auditEntries = await GetAuditEntriesAsync(factory);
        foreach (var eventType in new[]
        {
            AuditEventType.RoleCreated,
            AuditEventType.RoleUpdated,
            AuditEventType.RolePermissionsUpdated,
            AuditEventType.RoleDeleted
        })
        {
            Assert.Contains(auditEntries, entry =>
                entry.EventType == eventType.ToString()
                && entry.Outcome == nameof(AuditOutcome.Succeeded)
                && entry.TargetRoleId == createdRole.Id);
        }
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

        var auditEntries = await GetAuditEntriesAsync(factory);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordChange)
            && entry.Outcome == nameof(AuditOutcome.Failed));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordChange)
            && entry.Outcome == nameof(AuditOutcome.Succeeded));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.Login)
            && entry.Outcome == nameof(AuditOutcome.Failed));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.Login)
            && entry.Outcome == nameof(AuditOutcome.Succeeded));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.Logout)
            && entry.Outcome == nameof(AuditOutcome.Succeeded));
        AssertAuditEntriesContainNoSensitiveValues(
            auditEntries,
            factory.SuperAdminEmail,
            factory.SuperAdminPassword,
            "IncorrectPassword123!",
            "UpdatedPassword123!");
    }

    [Fact]
    public async Task Password_reset_uses_configured_public_url_and_does_not_disclose_unknown_email()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = await GetPageTokenAsync(client, "/Account/ForgotPassword");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/ForgotPassword")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = factory.SuperAdminEmail
            })
        };
        request.Headers.Host = "attacker.example";
        using var forgotResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);
        Assert.Contains(
            "Nếu email khớp với tài khoản",
            await forgotResponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var resetUrl = Assert.IsType<string>(factory.SentResetUrl);
        var resetUri = new Uri(resetUrl);
        Assert.Equal("basemvc.example.test", resetUri.Host);
        Assert.Equal("/Account/ResetPassword", resetUri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(resetUri.Query);
        var code = Assert.Single(query["code"]);
        var email = Assert.Single(query["email"]);
        Assert.NotNull(code);
        Assert.NotNull(email);
        Assert.Equal(factory.SuperAdminEmail, email);

        var confirmationContent = await forgotResponse.Content.ReadAsStringAsync();
        var unknownToken = await GetPageTokenAsync(client, "/Account/ForgotPassword");
        using var unknownResponse = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = unknownToken,
                ["Email"] = "unknown-account@example.test"
            }));
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        Assert.Equal(
            confirmationContent,
            await unknownResponse.Content.ReadAsStringAsync());
        Assert.Equal(factory.SuperAdminEmail, factory.LastSentRecipient);

        factory.FailEmailDelivery();
        var failedDeliveryToken = await GetPageTokenAsync(client, "/Account/ForgotPassword");
        using var failedDeliveryResponse = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = failedDeliveryToken,
                ["Email"] = factory.SuperAdminEmail
            }));
        Assert.Equal(HttpStatusCode.OK, failedDeliveryResponse.StatusCode);
        Assert.Equal(
            confirmationContent,
            await failedDeliveryResponse.Content.ReadAsStringAsync());

        var resetPageUrl = QueryHelpers.AddQueryString(
            "/Account/ResetPassword",
            new Dictionary<string, string?>
            {
                ["email"] = factory.SuperAdminEmail,
                ["code"] = code
            });
        var resetToken = await GetPageTokenAsync(client, resetPageUrl);
        using var resetResponse = await client.PostAsync(
            "/Account/ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = resetToken,
                ["Email"] = factory.SuperAdminEmail,
                ["Code"] = code,
                ["Password"] = "RecoveredPassword123!",
                ["ConfirmPassword"] = "RecoveredPassword123!"
            }));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.Contains(
            "Mật khẩu đã cập nhật",
            WebUtility.HtmlDecode(await resetResponse.Content.ReadAsStringAsync()),
            StringComparison.Ordinal);

        await AssertLoginResponseAsync(
            client,
            factory.SuperAdminEmail,
            factory.SuperAdminPassword,
            HttpStatusCode.OK);
        await AssertLoginResponseAsync(
            client,
            factory.SuperAdminEmail,
            "RecoveredPassword123!",
            HttpStatusCode.Redirect);

        var auditEntries = await GetAuditEntriesAsync(factory);
        Assert.Equal(
            3,
            auditEntries.Count(entry =>
                entry.EventType == nameof(AuditEventType.PasswordResetRequested)
                && entry.Outcome == nameof(AuditOutcome.Accepted)));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordResetRequested)
            && entry.Outcome == nameof(AuditOutcome.Accepted)
            && entry.TargetUserId is null);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordResetEmailDelivery)
            && entry.Outcome == nameof(AuditOutcome.Failed));
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordResetCompleted)
            && entry.Outcome == nameof(AuditOutcome.Succeeded));
        AssertAuditEntriesContainNoSensitiveValues(
            auditEntries,
            factory.SuperAdminEmail,
            code,
            resetUrl,
            "RecoveredPassword123!");
    }

    [Fact]
    public async Task Password_reset_requires_antiforgery_and_invalid_tokens_cannot_change_password()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var missingTokenResponse = await client.PostAsJsonAsync(
            "/Account/ForgotPassword",
            new { email = factory.SuperAdminEmail });
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        var token = await GetPageTokenAsync(client, "/Account/ForgotPassword");
        using var forgotResponse = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Email"] = factory.SuperAdminEmail
            }));
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var resetUrl = Assert.IsType<string>(factory.SentResetUrl);
        var resetQuery = QueryHelpers.ParseQuery(new Uri(resetUrl).Query);
        var resetCode = Assert.Single(resetQuery["code"]);
        Assert.NotNull(resetCode);
        var resetToken = await GetPageTokenAsync(
            client,
            QueryHelpers.AddQueryString(
                "/Account/ResetPassword",
                new Dictionary<string, string?>
                {
                    ["email"] = factory.SuperAdminEmail,
                    ["code"] = resetCode
                }));
        var invalidCode = Convert.ToBase64String("invalid-reset-token"u8.ToArray());
        using var invalidResetResponse = await client.PostAsync(
            "/Account/ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = resetToken,
                ["Email"] = factory.SuperAdminEmail,
                ["Code"] = invalidCode,
                ["Password"] = "InvalidAttempt123!",
                ["ConfirmPassword"] = "InvalidAttempt123!"
            }));
        Assert.Equal(HttpStatusCode.OK, invalidResetResponse.StatusCode);
        Assert.Contains(
            "không hợp lệ hoặc đã hết hạn",
            WebUtility.HtmlDecode(await invalidResetResponse.Content.ReadAsStringAsync()),
            StringComparison.Ordinal);

        await AssertLoginResponseAsync(
            client,
            factory.SuperAdminEmail,
            factory.SuperAdminPassword,
            HttpStatusCode.Redirect);

        var auditEntries = await GetAuditEntriesAsync(factory);
        Assert.Contains(auditEntries, entry =>
            entry.EventType == nameof(AuditEventType.PasswordResetCompleted)
            && entry.Outcome == nameof(AuditOutcome.Failed));
        AssertAuditEntriesContainNoSensitiveValues(
            auditEntries,
            factory.SuperAdminEmail,
            resetCode,
            invalidCode,
            "InvalidAttempt123!");
    }

    [Fact]
    public async Task Login_and_password_reset_requests_are_rate_limited_with_retry_after()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginToken = await GetPageTokenAsync(client, "/Account/Login");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var loginResponse = await client.PostAsync(
                "/Account/Login",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = loginToken,
                    ["Email"] = factory.SuperAdminEmail,
                    ["Password"] = "IncorrectPassword123!",
                    ["RememberMe"] = "false"
                }));
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }

        using var limitedLoginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = loginToken,
                ["Email"] = factory.SuperAdminEmail,
                ["Password"] = "IncorrectPassword123!",
                ["RememberMe"] = "false"
            }));
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedLoginResponse.StatusCode);
        Assert.NotNull(limitedLoginResponse.Headers.RetryAfter);

        var resetToken = await GetPageTokenAsync(client, "/Account/ForgotPassword");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var resetResponse = await client.PostAsync(
                "/Account/ForgotPassword",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = resetToken,
                    ["Email"] = $"unknown-{attempt}@example.test"
                }));
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        }

        using var limitedResetResponse = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = resetToken,
                ["Email"] = "unknown-three@example.test"
            }));
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResetResponse.StatusCode);
        Assert.NotNull(limitedResetResponse.Headers.RetryAfter);
    }

    private static async Task<string> GetPageTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
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

    private static async Task<List<AuditLogEntry>> GetAuditEntriesAsync(
        UserManagementWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.AuditLogEntries
            .AsNoTracking()
            .OrderBy(entry => entry.Id)
            .ToListAsync();
    }

    private static void AssertAuditEntriesContainNoSensitiveValues(
        IEnumerable<AuditLogEntry> auditEntries,
        params string?[] sensitiveValues)
    {
        var storedValues = auditEntries
            .SelectMany(entry => new[]
            {
                entry.EventType,
                entry.Outcome,
                entry.ActorUserId,
                entry.TargetUserId,
                entry.TargetRoleId
            })
            .Where(value => value is not null)
            .ToArray();

        foreach (var sensitiveValue in sensitiveValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            Assert.DoesNotContain(storedValues, value =>
                value!.Contains(sensitiveValue!, StringComparison.OrdinalIgnoreCase));
        }
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

    private sealed record RoleMutationResponse(string Id, string Name, int UserCount);

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
        private readonly FakeEmailSender _emailSender = new();

        public string SuperAdminEmail { get; } = "integration-superadmin@example.test";

        public string SuperAdminPassword { get; } = "IntegrationAdmin123!";

        public string? SentResetUrl => _emailSender.LastResetUrl;

        public string? LastSentRecipient => _emailSender.LastRecipient;

        public void FailEmailDelivery() => _emailSender.ShouldFail = true;

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
                    ["Email:SmtpHost"] = "localhost",
                    ["Email:SmtpPort"] = "587",
                    ["Email:FromAddress"] = "no-reply@basemvc.example.test",
                    ["Email:PublicBaseUrl"] = "https://basemvc.example.test",
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
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_emailSender);
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

        private sealed class FakeEmailSender : IEmailSender
        {
            public string? LastRecipient { get; private set; }

            public string? LastResetUrl { get; private set; }

            public bool ShouldFail { get; set; }

            public Task SendPasswordResetAsync(
                string recipient,
                string resetUrl,
                CancellationToken cancellationToken = default)
            {
                LastRecipient = recipient;
                LastResetUrl = resetUrl;
                if (ShouldFail)
                {
                    throw new EmailDeliveryException(
                        "Test email delivery failure.",
                        new InvalidOperationException("Simulated SMTP failure."));
                }

                return Task.CompletedTask;
            }
        }
    }
}
