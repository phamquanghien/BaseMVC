using BaseMVC.Application.Accounts;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace BaseMVC.Infrastructure.Identity;

public sealed class AuthenticationService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IAuthenticationService
{
    public async Task<LoginOutcome> LoginAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await auditLogWriter.WriteAsync(AuditEventType.Login, AuditOutcome.Failed, cancellationToken: cancellationToken);
            return LoginOutcome.Failed;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await auditLogWriter.WriteAsync(AuditEventType.Login, AuditOutcome.Succeeded,
                actorUserId: user.Id, targetUserId: user.Id, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoginOutcome.Succeeded;
        }

        var outcome = result.IsLockedOut ? AuditOutcome.LockedOut : AuditOutcome.Failed;
        await auditLogWriter.WriteAsync(AuditEventType.Login, outcome,
            targetUserId: user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result.IsLockedOut ? LoginOutcome.LockedOut : LoginOutcome.Failed;
    }

    public async Task LogoutAsync(string? userId, CancellationToken cancellationToken)
    {
        await signInManager.SignOutAsync();
        await auditLogWriter.WriteAsync(AuditEventType.Logout, AuditOutcome.Succeeded,
            actorUserId: userId, cancellationToken: cancellationToken);
    }
}
