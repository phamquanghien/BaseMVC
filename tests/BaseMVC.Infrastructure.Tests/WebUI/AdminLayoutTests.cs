using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BaseMVC.Infrastructure.Tests.WebUI;

public sealed partial class UserManagementEndpointTests
{
    [Fact]
    public async Task Admin_pages_use_admin_layout_and_public_pages_use_public_layout()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, factory.SuperAdminEmail, factory.SuperAdminPassword);

        var dashboard = await client.GetStringAsync("/Admin");
        Assert.Contains("id=\"admin-drawer\"", dashboard);
        Assert.Contains("href=\"/Admin/Students\"", dashboard);

        var students = await client.GetStringAsync("/Admin/Students");
        Assert.Contains("id=\"admin-drawer\"", students);

        var home = await client.GetStringAsync("/");
        Assert.DoesNotContain("id=\"admin-drawer\"", home);
        Assert.Contains("href=\"/Admin\"", home);
    }

    [Fact]
    public async Task Admin_dashboard_requires_login_and_public_layout_hides_admin_link_without_permissions()
    {
        using var factory = new UserManagementWebApplicationFactory();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var denied = await anonymous.GetAsync("/Admin");
        Assert.True(denied.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);

        const string email = "no-admin-access@example.test";
        const string password = "LimitedUser123!";
        await factory.CreateUserAsync(email, password);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(client, email, password);
        var home = await client.GetStringAsync("/");
        Assert.DoesNotContain("href=\"/Admin\"", home);
    }
}
