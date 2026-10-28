using MediatR;

namespace BaseMVC.Application.Accounts;

public interface IAccountPasswordService
{
    Task<PasswordResetPreparation> PrepareResetAsync(string email, CancellationToken cancellationToken);
    Task DeliverResetEmailAsync(string userId, string email, string resetUrl, CancellationToken cancellationToken);
    Task<PasswordOperationResult> ResetPasswordAsync(string email, string code, string password, CancellationToken cancellationToken);
    Task<PasswordOperationResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public sealed class PreparePasswordResetHandler(IAccountPasswordService service) : IRequestHandler<PreparePasswordResetCommand, PasswordResetPreparation>
{
    public Task<PasswordResetPreparation> Handle(PreparePasswordResetCommand request, CancellationToken cancellationToken) => service.PrepareResetAsync(request.Email, cancellationToken);
}

public sealed class DeliverPasswordResetEmailHandler(IAccountPasswordService service) : IRequestHandler<DeliverPasswordResetEmailCommand>
{
    public Task Handle(DeliverPasswordResetEmailCommand request, CancellationToken cancellationToken) => service.DeliverResetEmailAsync(request.UserId, request.Email, request.ResetUrl, cancellationToken);
}

public sealed class ResetPasswordHandler(IAccountPasswordService service) : IRequestHandler<ResetPasswordCommand, PasswordOperationResult>
{
    public Task<PasswordOperationResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken) => service.ResetPasswordAsync(request.Email, request.Code, request.Password, cancellationToken);
}

public sealed class ChangePasswordHandler(IAccountPasswordService service) : IRequestHandler<ChangePasswordCommand, PasswordOperationResult>
{
    public Task<PasswordOperationResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken) => service.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword, cancellationToken);
}
