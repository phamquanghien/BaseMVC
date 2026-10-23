using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Identity;

public sealed class ApplicationCookieEvents(
    ISecurityStampValidator securityStampValidator,
    UserManager<ApplicationUser> userManager,
    ILogger<ApplicationCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        await securityStampValidator.ValidateAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await userManager.GetUserAsync(context.Principal);
        if (user is not null && !await userManager.IsLockedOutAsync(user))
        {
            return;
        }

        logger.LogInformation(
            "Rejecting an application cookie because user {UserId} is missing or locked.",
            user?.Id);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
