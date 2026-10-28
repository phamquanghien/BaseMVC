using MediatR;

namespace BaseMVC.Application.Accounts;

public sealed record PasswordResetPreparation(string? UserId, string? Email, string? Token)
{
    public bool HasConfirmedUser => UserId is not null && Email is not null && Token is not null;
}

public sealed record PasswordOperationResult(bool Succeeded, IReadOnlyList<(string Code, string Description)> Errors);

public sealed record PreparePasswordResetCommand(string Email) : IRequest<PasswordResetPreparation>;
public sealed record DeliverPasswordResetEmailCommand(string UserId, string Email, string ResetUrl) : IRequest;
public sealed record ResetPasswordCommand(string Email, string Code, string Password) : IRequest<PasswordOperationResult>;
public sealed record ChangePasswordCommand(string UserId, string CurrentPassword, string NewPassword) : IRequest<PasswordOperationResult>;
