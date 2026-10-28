using BaseMVC.Application.Accounts;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BaseMVC.Infrastructure.Identity;

public sealed class AccountPasswordService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IPasswordResetEmailSender emailSender,
    ILogger<AccountPasswordService> logger) : IAccountPasswordService
{
    public async Task<PasswordResetPreparation> PrepareResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            await auditLogWriter.WriteAsync(AuditEventType.PasswordResetRequested, AuditOutcome.Accepted, cancellationToken: cancellationToken);
            return new PasswordResetPreparation(null, null, null);
        }
        await auditLogWriter.WriteAsync(AuditEventType.PasswordResetRequested, AuditOutcome.Accepted, targetUserId: user.Id, cancellationToken: cancellationToken);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return new PasswordResetPreparation(user.Id, user.Email!, token);
    }

    public async Task DeliverResetEmailAsync(string userId, string email, string resetUrl, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendPasswordResetAsync(email, resetUrl, cancellationToken);
        }
        catch (PasswordResetEmailDeliveryException exception)
        {
            logger.LogError(exception, "Password recovery email delivery failed.");
            await auditLogWriter.WriteAsync(AuditEventType.PasswordResetEmailDelivery, AuditOutcome.Failed,
                targetUserId: userId, cancellationToken: cancellationToken);
        }
    }

    public async Task<PasswordOperationResult> ResetPasswordAsync(string email, string code, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await auditLogWriter.WriteAsync(AuditEventType.PasswordResetCompleted, AuditOutcome.Failed, cancellationToken: cancellationToken);
            return new PasswordOperationResult(false, []);
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ResetPasswordAsync(user, code, password);
        if (!result.Succeeded)
        {
            await auditLogWriter.WriteAsync(AuditEventType.PasswordResetCompleted, AuditOutcome.Failed,
                targetUserId: user.Id, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PasswordOperationResult(false, result.Errors.Select(error => (error.Code, error.Description)).ToArray());
        }
        await auditLogWriter.WriteAsync(AuditEventType.PasswordResetCompleted, AuditOutcome.Succeeded,
            actorUserId: user.Id, targetUserId: user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordOperationResult(true, []);
    }

    public async Task<PasswordOperationResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return new PasswordOperationResult(false, []);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            await auditLogWriter.WriteAsync(AuditEventType.PasswordChange, AuditOutcome.Failed,
                actorUserId: user.Id, targetUserId: user.Id, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PasswordOperationResult(false, result.Errors.Select(error => (error.Code, error.Description)).ToArray());
        }
        await auditLogWriter.WriteAsync(AuditEventType.PasswordChange, AuditOutcome.Succeeded,
            actorUserId: user.Id, targetUserId: user.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await signInManager.RefreshSignInAsync(user);
        return new PasswordOperationResult(true, []);
    }
}
