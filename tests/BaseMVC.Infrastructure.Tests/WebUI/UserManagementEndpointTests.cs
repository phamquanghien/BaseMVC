using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.WebUI.SqliteBackups;
using BaseMVC.WebUI.Email;
using BaseMVC.Infrastructure.Authorization;
using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Auditing;
using BaseMVC.WebUI.HealthChecks;
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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var superAdminRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.SuperAdmin);
        const string secondSuperAdminEmail = "second-superadmin@example.test";
        await factory.CreateUserAsync(secondSuperAdminEmail, "SecondAdmin123!");
        await factory.AssignUserToRoleAsync(secondSuperAdminEmail, IdentitySeedRoles.SuperAdmin);

        using var changeSuperAdminEmailResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{superAdmin.Id}/edit",
            token,
            new
            {
                email = "renamed-superadmin@example.test",
                roleIds = new[] { superAdminRole.Id },
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.Conflict, changeSuperAdminEmailResponse.StatusCode);
        Assert.Contains(
            "Không thể thay đổi email của tài khoản có role SuperAdmin.",
            await changeSuperAdminEmailResponse.Content.ReadAsStringAsync());

        var unchangedSuperAdmin = await client.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=integration-superadmin%40example.test");
        Assert.NotNull(unchangedSuperAdmin);
        Assert.Equal(factory.SuperAdminEmail, Assert.Single(unchangedSuperAdmin.Items).Email);

        using var changeEmailAndRemoveSuperAdminRoleResponse = await PostWithTokenAsync(
            client,
            $"/Admin/Users/{superAdmin.Id}/edit",
            token,
            new
            {
                email = "demoted-superadmin@example.test",
                roleIds = Array.Empty<string>(),
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.Conflict, changeEmailAndRemoveSuperAdminRoleResponse.StatusCode);

        var unchangedSuperAdminAfterDemotionAttempt = await client.GetFromJsonAsync<UserSearchDto>(
            "/Admin/Users/search?search=integration-superadmin%40example.test");
        Assert.NotNull(unchangedSuperAdminAfterDemotionAttempt);
        var stillSuperAdmin = Assert.Single(unchangedSuperAdminAfterDemotionAttempt.Items);
        Assert.Equal(factory.SuperAdminEmail, stillSuperAdmin.Email);
        Assert.Contains(IdentitySeedRoles.SuperAdmin, stillSuperAdmin.RoleNames);

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
    public async Task User_management_preserves_an_unlocked_super_admin_when_other_super_admins_are_locked()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var setupClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(setupClient, factory.SuperAdminEmail, factory.SuperAdminPassword);

        var roles = await setupClient.GetFromJsonAsync<List<RoleDto>>("/Admin/Users/roles");
        Assert.NotNull(roles);
        var superAdminRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.SuperAdmin);
        var administratorRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.Administrator);

        const string secondSuperAdminEmail = "lock-test-superadmin@example.test";
        const string operatorEmail = "lock-test-operator@example.test";
        await factory.CreateUserAsync(secondSuperAdminEmail, "SecondAdmin123!");
        await factory.AssignUserToRoleAsync(secondSuperAdminEmail, IdentitySeedRoles.SuperAdmin);
        await factory.CreateUserAsync(operatorEmail, "OperatorPassword123!");
        await factory.AssignUserToRoleAsync(operatorEmail, IdentitySeedRoles.Administrator);
        await factory.GrantAccessManagementAsync(operatorEmail);

        var initialSuperAdmin = await FindUserAsync(setupClient, factory.SuperAdminEmail);
        var secondSuperAdmin = await FindUserAsync(setupClient, secondSuperAdminEmail);

        using var operatorClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(operatorClient, operatorEmail, "OperatorPassword123!");
        var operatorToken = await GetAntiforgeryTokenAsync(operatorClient);

        using var lockSecondSuperAdminResponse = await PostWithTokenAsync(
            operatorClient,
            $"/Admin/Users/{secondSuperAdmin.Id}/edit",
            operatorToken,
            new
            {
                email = secondSuperAdminEmail,
                roleIds = new[] { superAdminRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.OK, lockSecondSuperAdminResponse.StatusCode);
        var lockedSuperAdmin = await lockSecondSuperAdminResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(lockedSuperAdmin);
        Assert.True(lockedSuperAdmin.IsLocked);

        using var lockLastAvailableSuperAdminResponse = await PostWithTokenAsync(
            operatorClient,
            $"/Admin/Users/{initialSuperAdmin.Id}/edit",
            operatorToken,
            new
            {
                email = factory.SuperAdminEmail,
                roleIds = new[] { superAdminRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.Conflict, lockLastAvailableSuperAdminResponse.StatusCode);

        using var removeLastAvailableSuperAdminResponse = await PostWithTokenAsync(
            operatorClient,
            $"/Admin/Users/{initialSuperAdmin.Id}/edit",
            operatorToken,
            new
            {
                email = factory.SuperAdminEmail,
                roleIds = Array.Empty<string>(),
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.Conflict, removeLastAvailableSuperAdminResponse.StatusCode);

        using var deleteLastAvailableSuperAdminResponse = await PostWithTokenAsync(
            operatorClient,
            $"/Admin/Users/{initialSuperAdmin.Id}/delete",
            operatorToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteLastAvailableSuperAdminResponse.StatusCode);

        var stillAvailableSuperAdmin = await FindUserAsync(operatorClient, factory.SuperAdminEmail);
        var stillLockedSuperAdmin = await FindUserAsync(operatorClient, secondSuperAdminEmail);
        Assert.False(stillAvailableSuperAdmin.IsLocked);
        Assert.True(stillLockedSuperAdmin.IsLocked);

        using var unlockSecondSuperAdminResponse = await PostWithTokenAsync(
            operatorClient,
            $"/Admin/Users/{secondSuperAdmin.Id}/edit",
            operatorToken,
            new
            {
                email = secondSuperAdminEmail,
                roleIds = new[] { superAdminRole.Id },
                isLocked = false
            });
        Assert.Equal(HttpStatusCode.OK, unlockSecondSuperAdminResponse.StatusCode);
        var unlockedSuperAdmin = await unlockSecondSuperAdminResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(unlockedSuperAdmin);
        Assert.False(unlockedSuperAdmin.IsLocked);
        Assert.Contains(IdentitySeedRoles.SuperAdmin, unlockedSuperAdmin.RoleNames);

        var operatorAccount = await FindUserAsync(operatorClient, operatorEmail);
        Assert.Equal(administratorRole.Name, Assert.Single(operatorAccount.RoleNames));
    }

    [Fact]
    public async Task Locking_user_invalidates_their_existing_authenticated_session()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var administratorClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(administratorClient, factory.SuperAdminEmail, factory.SuperAdminPassword);

        const string targetEmail = "active-session-superadmin@example.test";
        await factory.CreateUserAsync(targetEmail, "TargetAdmin123!");
        await factory.AssignUserToRoleAsync(targetEmail, IdentitySeedRoles.SuperAdmin);
        var targetUser = await FindUserAsync(administratorClient, targetEmail);
        var roles = await administratorClient.GetFromJsonAsync<List<RoleDto>>("/Admin/Users/roles");
        Assert.NotNull(roles);
        var superAdminRole = Assert.Single(roles, role => role.Name == IdentitySeedRoles.SuperAdmin);
        var administratorToken = await GetAntiforgeryTokenAsync(administratorClient);

        using var targetClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(targetClient, targetEmail, "TargetAdmin123!");
        using var authorizedBeforeLockResponse = await targetClient.GetAsync("/Admin/Audit/search");
        Assert.Equal(HttpStatusCode.OK, authorizedBeforeLockResponse.StatusCode);

        using var lockResponse = await PostWithTokenAsync(
            administratorClient,
            $"/Admin/Users/{targetUser.Id}/edit",
            administratorToken,
            new
            {
                email = targetEmail,
                roleIds = new[] { superAdminRole.Id },
                isLocked = true
            });
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        using var responseAfterLock = await targetClient.GetAsync("/Admin/Audit/search");
        Assert.Equal(HttpStatusCode.Unauthorized, responseAfterLock.StatusCode);
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

    [Fact]
    public async Task Audit_log_is_permission_protected_and_supports_filters_and_server_side_paging()
    {
        using var factory = new UserManagementWebApplicationFactory();
        var regularUserEmail = "audit-reader-without-permission@example.test";
        await factory.CreateUserAsync(regularUserEmail, "RegularUser123!");

        using var unauthorizedClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(unauthorizedClient, regularUserEmail, "RegularUser123!");
        using var deniedResponse = await unauthorizedClient.GetAsync("/Admin/Audit/search");
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
            dbContext.AuditLogEntries.AddRange(Enumerable.Range(0, 12).Select(index =>
                new AuditLogEntry
                {
                    OccurredAtUtc = timestamp.AddSeconds(index),
                    EventType = index < 6
                        ? nameof(AuditEventType.Login)
                        : nameof(AuditEventType.UserUpdated),
                    Outcome = index % 2 == 0
                        ? nameof(AuditOutcome.Failed)
                        : nameof(AuditOutcome.Succeeded),
                    ActorUserId = $"audit-actor-{index}",
                    TargetUserId = index == 0 ? "audit-target-special" : $"audit-target-{index}"
                }));
            await dbContext.SaveChangesAsync();
        }

        using var indexResponse = await client.GetAsync("/Admin/Audit");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        using var pageResponse = await client.GetAsync(
            "/Admin/Audit/search?search=audit-actor-&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var page = await pageResponse.Content.ReadFromJsonAsync<AuditSearchDto>();
        Assert.NotNull(page);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(2, page.TotalPages);

        using var filterResponse = await client.GetAsync(
            "/Admin/Audit/search?eventType=Login&outcome=Failed&search=audit-target-special");
        Assert.Equal(HttpStatusCode.OK, filterResponse.StatusCode);
        var filtered = await filterResponse.Content.ReadFromJsonAsync<AuditSearchDto>();
        Assert.NotNull(filtered);
        var filteredEntry = Assert.Single(filtered.Items);
        Assert.Equal(nameof(AuditEventType.Login), filteredEntry.EventType);
        Assert.Equal(nameof(AuditOutcome.Failed), filteredEntry.Outcome);
        Assert.Equal("audit-target-special", filteredEntry.TargetUserId);

        using var invalidEventResponse = await client.GetAsync(
            "/Admin/Audit/search?eventType=EmailContainsSensitiveData");
        Assert.Equal(HttpStatusCode.BadRequest, invalidEventResponse.StatusCode);

        using var longSearchResponse = await client.GetAsync(
            $"/Admin/Audit/search?search={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, longSearchResponse.StatusCode);

        Assert.DoesNotContain(
            regularUserEmail,
            await filterResponse.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authorization_status_pages_distinguish_anonymous_and_forbidden_users_and_apply_role_changes_immediately()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var anonymousMvcResponse = await anonymousClient.GetAsync("/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.Redirect, anonymousMvcResponse.StatusCode);
        var loginUri = Assert.IsType<Uri>(anonymousMvcResponse.Headers.Location);
        Assert.Equal("/Account/Login", loginUri.AbsolutePath);
        Assert.Equal(
            "/Account/ChangePassword",
            Assert.Single(QueryHelpers.ParseQuery(loginUri.Query)["ReturnUrl"]));

        using var anonymousHtmlRequest = new HttpRequestMessage(HttpMethod.Get, "/Admin/Audit/search");
        anonymousHtmlRequest.Headers.Accept.ParseAdd("text/html");

        using var anonymousHtmlResponse = await anonymousClient.SendAsync(anonymousHtmlRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHtmlResponse.StatusCode);
        var unauthorizedPage = await anonymousHtmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("LỖI 401", unauthorizedPage);
        Assert.Contains("Bạn cần đăng nhập để tiếp tục", unauthorizedPage);
        Assert.Contains("/Account/Login?returnUrl=", unauthorizedPage);
        Assert.Contains("%2FAdmin%2FAudit%2Fsearch", unauthorizedPage);

        using var anonymousJsonRequest = new HttpRequestMessage(HttpMethod.Get, "/Admin/Audit/search");
        anonymousJsonRequest.Headers.Accept.ParseAdd("application/json");
        using var anonymousJsonResponse = await anonymousClient.SendAsync(anonymousJsonRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousJsonResponse.StatusCode);
        Assert.Equal("application/problem+json", anonymousJsonResponse.Content.Headers.ContentType?.MediaType);

        using var anonymousPostRequest = new HttpRequestMessage(HttpMethod.Post, "/Admin/Users")
        {
            Content = JsonContent.Create(new { email = "anonymous@example.test" })
        };
        anonymousPostRequest.Headers.Accept.ParseAdd("application/json");
        using var anonymousPostResponse = await anonymousClient.SendAsync(anonymousPostRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPostResponse.StatusCode);
        Assert.Equal("application/problem+json", anonymousPostResponse.Content.Headers.ContentType?.MediaType);

        const string userEmail = "audit-role-reader@example.test";
        const string roleName = "AuditRoleReader";
        await factory.CreateUserAsync(userEmail, "RegularUser123!");
        var roleId = await factory.CreateRoleForUserAsync(userEmail, roleName);

        using var roleClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SignInAsync(roleClient, userEmail, "RegularUser123!");

        var accessCases = new[]
        {
            (Permission: "Permissions.Users.Read", Page: "/Admin/Users", Api: "/Admin/Users/search"),
            (Permission: "Permissions.Roles.Read", Page: "/Admin/Roles", Api: "/Admin/Roles/search"),
            (
                Permission: "Permissions.Access.Read",
                Page: "/Admin/Permissions",
                Api: $"/Admin/Permissions/roles/{roleId}"),
            (Permission: "Permissions.Audit.Read", Page: "/Admin/Audit", Api: "/Admin/Audit/search")
        };

        foreach (var accessCase in accessCases)
        {
            using var deniedPageRequest = new HttpRequestMessage(HttpMethod.Get, accessCase.Page);
            deniedPageRequest.Headers.Accept.ParseAdd("text/html");
            using var deniedPageResponse = await roleClient.SendAsync(deniedPageRequest);
            Assert.Equal(HttpStatusCode.Forbidden, deniedPageResponse.StatusCode);
            Assert.Contains("LỖI 403", await deniedPageResponse.Content.ReadAsStringAsync());

            using var deniedApiResponse = await roleClient.GetAsync(accessCase.Api);
            Assert.Equal(HttpStatusCode.Forbidden, deniedApiResponse.StatusCode);

            await factory.SetRolePermissionAsync(roleName, accessCase.Permission, grant: true);
            using var grantedPageResponse = await roleClient.GetAsync(accessCase.Page);
            Assert.Equal(HttpStatusCode.OK, grantedPageResponse.StatusCode);
            using var grantedApiResponse = await roleClient.GetAsync(accessCase.Api);
            Assert.Equal(HttpStatusCode.OK, grantedApiResponse.StatusCode);

            await factory.SetRolePermissionAsync(roleName, accessCase.Permission, grant: false);
            using var revokedApiResponse = await roleClient.GetAsync(accessCase.Api);
            Assert.Equal(HttpStatusCode.Forbidden, revokedApiResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_retention_deletes_only_entries_older_than_90_days()
    {
        using var factory = new UserManagementWebApplicationFactory();
        var nowUtc = DateTimeOffset.UtcNow;
        var cutoff = nowUtc.AddDays(-AuditLogRetentionService.RetentionDays);
        long expiredId;
        long boundaryId;
        long recentId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var expired = new AuditLogEntry
            {
                OccurredAtUtc = cutoff.AddTicks(-1),
                EventType = nameof(AuditEventType.Login),
                Outcome = nameof(AuditOutcome.Failed)
            };
            var boundary = new AuditLogEntry
            {
                OccurredAtUtc = cutoff,
                EventType = nameof(AuditEventType.Login),
                Outcome = nameof(AuditOutcome.Succeeded)
            };
            var recent = new AuditLogEntry
            {
                OccurredAtUtc = cutoff.AddTicks(1),
                EventType = nameof(AuditEventType.Login),
                Outcome = nameof(AuditOutcome.Succeeded)
            };
            dbContext.AuditLogEntries.AddRange(expired, boundary, recent);
            await dbContext.SaveChangesAsync();
            expiredId = expired.Id;
            boundaryId = boundary.Id;
            recentId = recent.Id;
        }

        var retentionService = new AuditLogRetentionService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogRetentionService>.Instance);
        var deletedCount = await retentionService.DeleteExpiredEntriesAsync(nowUtc);

        Assert.Equal(1, deletedCount);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.False(await verificationContext.AuditLogEntries.AnyAsync(entry => entry.Id == expiredId));
        Assert.True(await verificationContext.AuditLogEntries.AnyAsync(entry => entry.Id == boundaryId));
        Assert.True(await verificationContext.AuditLogEntries.AnyAsync(entry => entry.Id == recentId));
    }

    [Fact]
    public async Task Sqlite_backup_is_integrity_checked_restorable_and_prunes_expired_backups()
    {
        var testDirectory = Directory.CreateTempSubdirectory("basemvc-backup-test-");
        try
        {
            var sourcePath = Path.Combine(testDirectory.FullName, "source.db");
            var backupDirectory = Path.Combine(testDirectory.FullName, "backups");
            var archiveDirectory = Path.Combine(testDirectory.FullName, "archive");
            var restoredPath = Path.Combine(testDirectory.FullName, "restored.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Pooling = false
            }.ToString();
            await using (var sourceConnection = new SqliteConnection(connectionString))
            {
                await sourceConnection.OpenAsync();
                await using var createProbe = sourceConnection.CreateCommand();
                createProbe.CommandText =
                    "CREATE TABLE BackupProbe (Value TEXT NOT NULL); INSERT INTO BackupProbe (Value) VALUES ('restore-verified');";
                await createProbe.ExecuteNonQueryAsync();
            }

            var expiredBackupPath = Path.Combine(backupDirectory, "basemvc-expired.db");
            Directory.CreateDirectory(backupDirectory);
            await File.WriteAllTextAsync(expiredBackupPath, "expired");
            File.SetLastWriteTimeUtc(expiredBackupPath, DateTime.UtcNow.AddDays(-8));
            var expiredArchivePath = Path.Combine(archiveDirectory, "basemvc-expired.db");
            Directory.CreateDirectory(archiveDirectory);
            await File.WriteAllTextAsync(expiredArchivePath, "expired");
            File.SetLastWriteTimeUtc(expiredArchivePath, DateTime.UtcNow.AddDays(-8));
            var unrelatedFilePath = Path.Combine(backupDirectory, "unrelated.db");
            await File.WriteAllTextAsync(unrelatedFilePath, "keep");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
            await using var provider = services.BuildServiceProvider();
            var hostEnvironment = new BackupTestHostEnvironment(testDirectory.FullName);
            var backupStatus = new SqliteBackupStatus();
            var archiveStore = new FileSystemBackupArchiveStore(
                NullLogger<FileSystemBackupArchiveStore>.Instance);
            var backupService = new SqliteDatabaseBackupService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                archiveStore,
                backupStatus,
                Options.Create(new SqliteBackupOptions
                {
                    Directory = backupDirectory,
                    ArchiveDirectory = archiveDirectory,
                    RetentionDays = 7
                }),
                hostEnvironment,
                NullLogger<SqliteDatabaseBackupService>.Instance);
            var backupOptions = new SqliteBackupOptions
            {
                Directory = backupDirectory,
                ArchiveDirectory = archiveDirectory,
                IntervalHours = 24,
                RetentionDays = 7
            };

            var backupPath = await backupService.CreateBackupAsync(DateTimeOffset.UtcNow);
            var archivedPath = Path.Combine(archiveDirectory, Path.GetFileName(backupPath));
            Assert.True(File.Exists(backupPath));
            Assert.True(File.Exists(archivedPath));
            Assert.False(File.Exists(expiredBackupPath));
            Assert.False(File.Exists(expiredArchivePath));
            Assert.True(File.Exists(unrelatedFilePath));

            var healthCheck = new SqliteBackupHealthCheck(
                Options.Create(backupOptions),
                backupStatus,
                hostEnvironment,
                NullLogger<SqliteBackupHealthCheck>.Instance);
            var healthCheckContext = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(
                    "sqlite-backup",
                    _ => healthCheck,
                    HealthStatus.Unhealthy,
                    ["ready"])
            };
            var healthyResult = await healthCheck.CheckHealthAsync(healthCheckContext);
            Assert.Equal(HealthStatus.Healthy, healthyResult.Status);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(backupDirectory),
                path => Path.GetFileName(path).StartsWith(
                    ".basemvc-backup-health-",
                    StringComparison.Ordinal));

            backupStatus.RecordFailure();
            var failedAttemptResult = await healthCheck.CheckHealthAsync(healthCheckContext);
            Assert.Equal(HealthStatus.Unhealthy, failedAttemptResult.Status);
            backupStatus.RecordSuccess(DateTimeOffset.UtcNow);

            File.SetLastWriteTimeUtc(backupPath, DateTime.UtcNow.AddHours(-49));
            File.SetLastWriteTimeUtc(archivedPath, DateTime.UtcNow.AddHours(-49));
            var staleResult = await healthCheck.CheckHealthAsync(healthCheckContext);
            Assert.Equal(HealthStatus.Unhealthy, staleResult.Status);

            var missingDirectoryCheck = new SqliteBackupHealthCheck(
                Options.Create(new SqliteBackupOptions
                {
                    Directory = Path.Combine(testDirectory.FullName, "missing")
                }),
                backupStatus,
                hostEnvironment,
                NullLogger<SqliteBackupHealthCheck>.Instance);
            var missingDirectoryResult = await missingDirectoryCheck.CheckHealthAsync(healthCheckContext);
            Assert.Equal(HealthStatus.Unhealthy, missingDirectoryResult.Status);

            await using var backupConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = archivedPath,
                    Pooling = false
                }.ToString());
            await backupConnection.OpenAsync();
            await using var restoredConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = restoredPath,
                    Pooling = false
                }.ToString());
            await restoredConnection.OpenAsync();
            backupConnection.BackupDatabase(restoredConnection);

            await using var verifyRestore = restoredConnection.CreateCommand();
            verifyRestore.CommandText = "SELECT Value FROM BackupProbe;";
            Assert.Equal("restore-verified", await verifyRestore.ExecuteScalarAsync());

            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(backupPath));
            }
        }
        finally
        {
            testDirectory.Delete(recursive: true);
        }
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

    private static async Task<UserDto> FindUserAsync(HttpClient client, string email)
    {
        var result = await client.GetFromJsonAsync<UserSearchDto>(
            $"/Admin/Users/search?search={Uri.EscapeDataString(email)}");
        Assert.NotNull(result);
        return Assert.Single(result.Items);
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

    private sealed record AuditSearchDto(
        IReadOnlyList<AuditListItemDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record AuditListItemDto(
        DateTimeOffset OccurredAtUtc,
        string EventType,
        string Outcome,
        string? ActorUserId,
        string? TargetUserId,
        string? TargetRoleId);

    private sealed class BackupTestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "BaseMVC.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;
    }

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

        public async Task CreateUserAsync(string email, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await userManager.CreateAsync(new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            }, password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        public async Task<string> CreateRoleForUserAsync(string email, string roleName)
        {
            using var scope = Services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var role = new IdentityRole(roleName);
            var roleResult = await roleManager.CreateAsync(role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            var assignmentResult = await userManager.AddToRoleAsync(user, roleName);
            Assert.True(
                assignmentResult.Succeeded,
                string.Join("; ", assignmentResult.Errors.Select(error => error.Description)));
            return role.Id;
        }

        public async Task AssignUserToRoleAsync(string email, string roleName)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var result = await userManager.AddToRoleAsync(user, roleName);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        public async Task SetRolePermissionAsync(string roleName, string permission, bool grant)
        {
            using var scope = Services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = await roleManager.FindByNameAsync(roleName);
            Assert.NotNull(role);

            var claims = await roleManager.GetClaimsAsync(role);
            var existingClaim = claims.FirstOrDefault(claim =>
                claim.Type == PermissionClaim.Type && claim.Value == permission);

            if (grant && existingClaim is null)
            {
                var addResult = await roleManager.AddClaimAsync(role, new Claim(PermissionClaim.Type, permission));
                Assert.True(addResult.Succeeded, string.Join("; ", addResult.Errors.Select(error => error.Description)));
            }
            else if (!grant && existingClaim is not null)
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, existingClaim);
                Assert.True(
                    removeResult.Succeeded,
                    string.Join("; ", removeResult.Errors.Select(error => error.Description)));
            }
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
                    ["SqliteBackup:Enabled"] = "false",
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
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    foreach (var registration in options.Registrations
                        .Where(registration => registration.Name == "sqlite-backup")
                        .ToArray())
                    {
                        options.Registrations.Remove(registration);
                    }
                });
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
