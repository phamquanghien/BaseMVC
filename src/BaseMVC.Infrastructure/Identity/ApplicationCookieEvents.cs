using System.Security.Claims;
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
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await userManager.GetUserAsync(principal);
        var securityStamp = principal.FindFirstValue(
            userManager.Options.ClaimsIdentity.SecurityStampClaimType);
        if (user is not null
            && !await userManager.IsLockedOutAsync(user)
            && !string.IsNullOrEmpty(user.SecurityStamp)
            && string.Equals(securityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            await securityStampValidator.ValidateAsync(context);
            return;
        }

        logger.LogInformation(
            "Rejecting an application cookie because user {UserId} is missing, locked, or has a changed security stamp.",
            user?.Id);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
